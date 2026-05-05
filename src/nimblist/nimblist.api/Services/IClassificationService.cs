namespace Nimblist.api.Services
{
    public interface IClassificationService
    {
        Task<(Guid? CategoryId, Guid? SubCategoryId)> ClassifyAsync(string itemName);
    }
}
