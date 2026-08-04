<?php

use App\Http\Controllers\AdminController;
use App\Http\Middleware\EnsureApiSession;
use Illuminate\Support\Facades\Route;

Route::get('/login', [AdminController::class, 'showLogin'])->name('login');
Route::post('/login', [AdminController::class, 'login'])->name('login.submit');

Route::middleware(EnsureApiSession::class)->group(function () {
    Route::get('/', [AdminController::class, 'dashboard'])->name('dashboard');
    Route::post('/logout', [AdminController::class, 'logout'])->name('logout');

    Route::get('/employees', [AdminController::class, 'employees'])->middleware('api.role:Admin,Manager,Director,Accountant')->name('employees');
    Route::post('/employees', [AdminController::class, 'storeEmployee'])->middleware('api.role:Admin')->name('employees.store');

    Route::get('/sites', [AdminController::class, 'sites'])->middleware('api.role:Admin,Manager,Director,Accountant')->name('sites');
    Route::post('/sites', [AdminController::class, 'storeSite'])->middleware('api.role:Admin')->name('sites.store');

    Route::get('/users', [AdminController::class, 'users'])->middleware('api.role:Admin')->name('users');
    Route::post('/users', [AdminController::class, 'storeUser'])->middleware('api.role:Admin')->name('users.store');

    Route::get('/manual', [AdminController::class, 'manualForm'])->middleware('api.role:Admin,Manager')->name('manual');
    Route::post('/manual', [AdminController::class, 'storeManual'])->middleware('api.role:Admin,Manager')->name('manual.store');

    Route::get('/reports', [AdminController::class, 'reports'])->middleware('api.role:Admin,Manager,Director,Accountant')->name('reports');
    Route::get('/reports/export', [AdminController::class, 'export'])->middleware('api.role:Admin,Manager,Director,Accountant')->name('reports.export');
});
