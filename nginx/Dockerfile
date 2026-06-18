FROM node:22-alpine AS build-admin
RUN corepack enable && corepack prepare pnpm@latest --activate
WORKDIR /app
COPY apps/admin/package.json apps/admin/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY apps/admin/ .
RUN VITE_API_BASE_URL= pnpm run build

FROM nginx:alpine
COPY --from=build-admin /app/dist /usr/share/nginx/html/admin
COPY nginx/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
