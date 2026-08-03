FROM composer:2 AS vendor
WORKDIR /app
COPY src/Admin/composer.json src/Admin/composer.lock ./
RUN composer install --no-dev --prefer-dist --no-interaction --no-scripts --optimize-autoloader

COPY src/Admin/app ./app
COPY src/Admin/bootstrap ./bootstrap
COPY src/Admin/config ./config
COPY src/Admin/database ./database
COPY src/Admin/public ./public
COPY src/Admin/resources ./resources
COPY src/Admin/routes ./routes
COPY src/Admin/artisan ./artisan
COPY src/Admin/phpunit.xml ./phpunit.xml
RUN composer dump-autoload --optimize \
    && mkdir -p storage/framework/cache storage/framework/sessions storage/framework/views storage/logs bootstrap/cache \
    && chmod -R 777 storage bootstrap/cache

FROM php:8.4-cli-bookworm AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libzip-dev unzip \
    && docker-php-ext-install pcntl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=vendor /app /app
ENV APP_ENV=production
EXPOSE 8080
CMD ["php", "-S", "0.0.0.0:8080", "-t", "public", "public/router.php"]
