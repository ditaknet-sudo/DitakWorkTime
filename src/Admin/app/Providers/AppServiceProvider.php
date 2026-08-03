<?php

namespace App\Providers;

use Illuminate\Support\Facades\URL;
use Illuminate\Support\ServiceProvider;

class AppServiceProvider extends ServiceProvider
{
    public function register(): void
    {
        //
    }

    public function boot(): void
    {
        $root = config('app.url');
        if (is_string($root) && $root !== '') {
            URL::forceRootUrl($root);
        }
    }
}
