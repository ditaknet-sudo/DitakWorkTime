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
    && chmod -R ug+rwX storage bootstrap/cache

FROM php:8.4-apache-bookworm AS final
WORKDIR /app
COPY deploy/apache/servername.conf /etc/apache2/conf-available/servername.conf
RUN a2enmod rewrite headers \
    && a2enconf servername
COPY --from=vendor /app /app
COPY deploy/apache/ditak-admin.conf /etc/apache2/sites-available/000-default.conf
RUN chown -R www-data:www-data storage bootstrap/cache
ENV APP_ENV=production
EXPOSE 80
