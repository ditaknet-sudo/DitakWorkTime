FROM node:22-alpine AS build
WORKDIR /app
COPY src/Web/package.json src/Web/package-lock.json* ./
RUN npm ci
COPY src/Web/ ./
ENV VITE_API_BASE=/api
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY src/Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
