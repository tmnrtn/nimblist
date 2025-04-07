# Dockerfile located in src/nimblist/Nimblist.Frontend/
# Build Context = Solution Root (.)

# ---- Build Stage ----
FROM node:20-alpine AS builder
WORKDIR /app
ENV NODE_ENV production

# Copy package files first, using path relative to context root
COPY ["src/nimblist/Nimblist.Frontend/package.json", "./"]
COPY ["src/nimblist/Nimblist.Frontend/package-lock.json", "./"]
# If using yarn:
# COPY ["src/nimblist/Nimblist.Frontend/yarn.lock", "./"]

# Install dependencies based on copied package files
RUN npm ci
# If using yarn:
# RUN yarn install --frozen-lockfile

# Copy the rest of the frontend application source code
# Source path is relative to context root
# Destination path is relative to WORKDIR /app
COPY ["src/nimblist/Nimblist.Frontend/", "./"]

# Build the React application
RUN npm run build
# If using yarn:
# RUN yarn build

# ---- Runtime Stage ----
FROM nginx:stable-alpine AS runtime
WORKDIR /usr/share/nginx/html

# Remove default Nginx public files
RUN rm -rf ./*

# Copy built static files from the builder stage
COPY --from=builder /app/dist .

# Copy the custom Nginx configuration file
# Assuming nginx.default.conf is located inside src/nimblist/Nimblist.Frontend/
# Path relative to context root
COPY ["src/nimblist/Nimblist.Frontend/nginx.default.conf", "/etc/nginx/conf.d/default.conf"]

# Expose port 80
EXPOSE 80

# Default command from nginx image starts the server
