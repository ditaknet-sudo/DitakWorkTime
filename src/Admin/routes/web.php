<?php

use App\Http\Controllers\AdminController;
use App\Http\Middleware\EnsureApiSession;
use Illuminate\Support\Facades\Route;

Route::get('/login', [AdminController::class, 'showLogin'])->name('login');
Route::post('/login', [AdminController::class, 'login'])->name('login.submit');

Route::middleware(EnsureApiSession::class)->group(function () {
    Route::get('/', [AdminController::class, 'dashboard'])->name('dashboard');
    Route::post('/logout', [AdminController::class, 'logout'])->name('logout');

    Route::get('/employees', [AdminController::class, 'employees'])->name('employees');
    Route::post('/employees', [AdminController::class, 'storeEmployee'])->name('employees.store');

    Route::get('/sites', [AdminController::class, 'sites'])->name('sites');
    Route::post('/sites', [AdminController::class, 'storeSite'])->name('sites.store');

    Route::get('/users', [AdminController::class, 'users'])->name('users');

    Route::get('/manual', [AdminController::class, 'manualForm'])->name('manual');
    Route::post('/manual', [AdminController::class, 'storeManual'])->name('manual.store');

    Route::get('/reports', [AdminController::class, 'reports'])->name('reports');
    Route::get('/reports/export', [AdminController::class, 'export'])->name('reports.export');
});
