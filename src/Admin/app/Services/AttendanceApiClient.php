<?php

namespace App\Services;

use Illuminate\Support\Facades\Http;
use RuntimeException;

class AttendanceApiClient
{
    public function __construct(private readonly string $baseUrl)
    {
    }

    public static function fromConfig(): self
    {
        return new self(rtrim((string) config('services.attendance.base_url', 'http://api:8080'), '/'));
    }

    public function login(string $email, string $password): array
    {
        $response = Http::timeout(20)->post($this->baseUrl.'/api/auth/login', [
            'email' => $email,
            'password' => $password,
        ]);

        if (! $response->successful()) {
            throw new RuntimeException('Invalid email or password');
        }

        return $response->json();
    }

    public function me(string $token): array
    {
        return $this->get($token, '/api/me');
    }

    public function employees(string $token): array
    {
        return $this->get($token, '/api/admin/employees');
    }

    public function createEmployee(string $token, array $payload): array
    {
        return $this->post($token, '/api/admin/employees', $payload);
    }

    public function sites(string $token): array
    {
        return $this->get($token, '/api/admin/sites');
    }

    public function createSite(string $token, array $payload): array
    {
        return $this->post($token, '/api/admin/sites', $payload);
    }

    public function users(string $token): array
    {
        return $this->get($token, '/api/admin/users');
    }

    public function createUser(string $token, array $payload): array
    {
        return $this->post($token, '/api/admin/users', $payload);
    }

    public function manualAttendance(string $token, array $payload): array
    {
        return $this->post($token, '/api/admin/attendance/manual', $payload);
    }

    public function monthlyReport(string $token, string $employeeId, int $year, int $month): array
    {
        return $this->get($token, "/api/reports/employees/{$employeeId}/monthly?year={$year}&month={$month}");
    }

    public function exportBinary(string $token, string $employeeId, int $year, int $month, string $format): string
    {
        $response = Http::withToken($token)
            ->timeout(60)
            ->get($this->baseUrl.'/api/reports/export', [
                'employeeId' => $employeeId,
                'year' => $year,
                'month' => $month,
                'format' => $format,
            ]);

        if (! $response->successful()) {
            throw new RuntimeException('Export failed: '.$response->body());
        }

        return $response->body();
    }

    private function get(string $token, string $path): array
    {
        $response = Http::withToken($token)->timeout(30)->get($this->baseUrl.$path);
        if (! $response->successful()) {
            throw new RuntimeException('API GET failed ('.$response->status().'): '.$response->body());
        }

        return $response->json() ?? [];
    }

    private function post(string $token, string $path, array $payload): array
    {
        $response = Http::withToken($token)->timeout(30)->post($this->baseUrl.$path, $payload);
        if (! $response->successful()) {
            throw new RuntimeException('API POST failed ('.$response->status().'): '.$response->body());
        }

        return $response->json() ?? [];
    }
}
