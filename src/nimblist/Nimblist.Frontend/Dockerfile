# Dockerfile (located inside nimblist.frontend folder)

# ---- Build Stage ----
    FROM node:20-alpine AS builder
    WORKDIR /app
    
    ENV NODE_ENV production
    
    # Copy package files from the build context root (which is nimblist.frontend)
    COPY package.json ./
    COPY package-lock.json ./
    # If using yarn:
    # COPY yarn.lock ./
    
    # Install dependencies
    RUN npm ci
    # If using yarn:
    # RUN yarn install --frozen-lockfile
    
    # Copy the rest of the application source code from the build context root
    COPY . ./
    
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
    # Adjust '/app/build' if your build output folder is different (e.g., '/app/dist')
    COPY --from=builder /app/dist .
    
    # Copy the custom Nginx configuration file
    # ** IMPORTANT: This assumes nginx.default.conf is NOW ALSO located **
    # ** inside the nimblist.frontend folder alongside this Dockerfile. **
    COPY nginx.default.conf /etc/nginx/conf.d/default.conf
    
    # Expose port 80
    EXPOSE 80
    
    # Default command from nginx image starts the server
    # CMD ["nginx", "-g", "daemon off;"]