# Stage 1: Build Angular app
FROM node:20-alpine AS build
WORKDIR /app

# Build configuration: production (default) or development
ARG NG_CONFIG=production

# Copy package files first for layer caching
COPY package*.json ./
RUN npm ci --legacy-peer-deps

# Copy source and build with specified configuration
COPY . .
RUN npx ng build --configuration=${NG_CONFIG}

# Stage 2: Serve with nginx
FROM nginx:alpine AS runtime

# Copy build output
COPY --from=build /app/dist/investment-mate-frontend /usr/share/nginx/html

# Copy custom nginx config for Angular routing support
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
