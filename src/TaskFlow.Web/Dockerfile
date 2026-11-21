FROM node:26-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
# package-lock.json is maintained on Windows; npm ci on Linux can fail on
# platform-specific optional native deps (@emnapi/*, oxide-linux-*).
# All dependencies in package.json are pinned exact, so npm install is deterministic.
RUN npm install --no-audit --no-fund

COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/taskflow/browser /usr/share/nginx/html

EXPOSE 80
