using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nimblist.api.Controllers;
using Nimblist.api.DTO;
using Nimblist.Data.Models;
using Xunit;

namespace Nimblist.test.Controllers
{
    public class AuthControllerTests
    {
        private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
        private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
        private readonly Mock<IUserStore<ApplicationUser>> _mockUserStore; // Needed for UserManager mock
        private readonly ILogger<AuthController> _nullLogger; // Use NullLogger for simplicity
        private readonly AuthController _controller;

        public AuthControllerTests()
        {
            _nullLogger = new NullLogger<AuthController>();
            _mockUserStore = new Mock<IUserStore<ApplicationUser>>();

            // Mock UserManager dependencies
            _mockUserManager = new Mock<UserManager<ApplicationUser>>(
                _mockUserStore.Object, // IUserStore<TUser> store
                null, // IOptions<IdentityOptions> optionsAccessor
                null, // IPasswordHasher<TUser> passwordHasher
                null, // IEnumerable<IUserValidator<TUser>> userValidators
                null, // IEnumerable<IPasswordValidator<TUser>> passwordValidators
                null, // ILookupNormalizer keyNormalizer
                null, // IdentityErrorDescriber errors
                null, // IServiceProvider services
                null  // ILogger<UserManager<TUser>> logger
            );

            // Mock SignInManager dependencies (needs more mocks than UserManager)
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            var mockUserClaimsPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();

            _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
                _mockUserManager.Object,              // UserManager<TUser> userManager
                mockHttpContextAccessor.Object,       // IHttpContextAccessor contextAccessor
                mockUserClaimsPrincipalFactory.Object,// IUserClaimsPrincipalFactory<TUser> claimsFactory
                null, // IOptions<IdentityOptions> optionsAccessor
                null, // ILogger<SignInManager<TUser>> logger
                null, // IAuthenticationSchemeProvider schemes
                null  // IUserConfirmation<TUser> confirmation
            );

            // Instantiate the controller with mocks
            _controller = new AuthController(
                _mockUserManager.Object,
                _mockSignInManager.Object,
                _nullLogger
            );
        }

        // Helper to create a ClaimsPrincipal for testing
        private ClaimsPrincipal CreateClaimsPrincipal(string userId, string email, bool isAuthenticated = true)
        {
            var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            // Add other claims if your code uses them
        };
            var identity = new ClaimsIdentity(claims, isAuthenticated ? "TestAuthType" : null);
            return new ClaimsPrincipal(identity);
        }

        // Helper to set the User on the ControllerContext
        private void SetControllerUser(string userId, string email, bool isAuthenticated = true)
        {
            var userPrincipal = CreateClaimsPrincipal(userId, email, isAuthenticated);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userPrincipal }
            };
        }

        // --- GetUserInfo Tests ---

        [Fact]
        public async Task GetUserInfo_UserIsAuthenticatedAndFound_ReturnsOkWithUserInfo()
        {
            // Arrange
            var userId = "test-id-123";
            var userEmail = "test@example.com";
            var testUser = new ApplicationUser { Id = userId, Email = userEmail, UserName = userEmail };

            // Setup UserManager mock
            _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                            .ReturnsAsync(testUser);

            // Set the authenticated user context on the controller
            SetControllerUser(userId, userEmail);

            // Act
            var result = await _controller.GetUserInfo();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var userInfo = Assert.IsType<UserInfoDto>(okResult.Value);

            Assert.Equal(userId, userInfo.UserId);
            Assert.Equal(userEmail, userInfo.Email);

            _mockUserManager.Verify(um => um.GetUserAsync(It.Is<ClaimsPrincipal>(cp => cp.Identity.IsAuthenticated)), Times.Once);
        }

        [Fact]
        public async Task GetUserInfo_UserIsAuthenticatedButNotFoundInStore_ReturnsUnauthorized()
        {
            // Arrange
            var userId = "ghost-id-404";
            var userEmail = "ghost@example.com";

            _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                            .ReturnsAsync((ApplicationUser)null);

            SetControllerUser(userId, userEmail);

            // Act
            var result = await _controller.GetUserInfo();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var actualValueObject = unauthorizedResult.Value; // Get the object from the result

            Assert.NotNull(actualValueObject); // Ensure it's not null

            // --- Use reflection to compare the property value ---
            Type actualValueType = actualValueObject.GetType();
            System.Reflection.PropertyInfo messageProperty = actualValueType.GetProperty("message");

            Assert.NotNull(messageProperty); // Verify the 'message' property exists

            string actualMessage = messageProperty.GetValue(actualValueObject)?.ToString(); // Get the property's value
            string expectedMessage = "User not found despite valid authentication.";

            Assert.Equal(expectedMessage, actualMessage); // Compare the string values directly
                                                          // --------------------------------------------------

            _mockUserManager.Verify(um => um.GetUserAsync(It.Is<ClaimsPrincipal>(cp => cp.Identity.IsAuthenticated)), Times.Once);
        }

        // Note: Testing the scenario where the user is *not* authenticated (e.g., no cookie)
        // is usually handled by integration tests that exercise the [Authorize] attribute filter.
        // Unit tests typically assume the filter has passed if testing code *inside* an [Authorize] method.

        // --- Logout Tests ---

        [Fact]
        public async Task Logout_UserIsAuthenticated_CallsSignOutAndReturnsOk()
        {
            // Arrange
            var userId = "logout-user-id";
            var userEmail = "logout@example.com";

            _mockSignInManager.Setup(sm => sm.SignOutAsync())
                              .Returns(Task.CompletedTask);

            SetControllerUser(userId, userEmail);

            // Act
            var result = await _controller.Logout();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var actualValueObject = okResult.Value; // Get the object from the result

            Assert.NotNull(actualValueObject); // Ensure it's not null

            // --- Use reflection to compare the property value ---
            Type actualValueType = actualValueObject.GetType();
            System.Reflection.PropertyInfo messageProperty = actualValueType.GetProperty("message");

            Assert.NotNull(messageProperty); // Verify the 'message' property exists

            string actualMessage = messageProperty.GetValue(actualValueObject)?.ToString(); // Get the property's value
            string expectedMessage = "Logout successful";

            Assert.Equal(expectedMessage, actualMessage); // Compare the string values directly
                                                          // --------------------------------------------------


            // Verify SignOutAsync was called
            _mockSignInManager.Verify(sm => sm.SignOutAsync(), Times.Once);
        }

        // Note: Similar to GetUserInfo, testing Logout when the user *isn't* authenticated
        // is typically an integration test concern due to the [Authorize] attribute.
    }
}