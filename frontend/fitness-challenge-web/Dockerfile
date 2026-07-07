# syntax=docker/dockerfile:1
# Build context is ./frontend/fitness-challenge-web

# ---- build ----
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# ---- runtime ----
FROM nginx:alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/fitness-challenge-web/browser /usr/share/nginx/html
EXPOSE 80
