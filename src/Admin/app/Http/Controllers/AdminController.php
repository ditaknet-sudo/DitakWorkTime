<?php

namespace App\Http\Controllers;

use App\Services\AttendanceApiClient;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Session;
use RuntimeException;
use Symfony\Component\HttpFoundation\StreamedResponse;

class AdminController extends Controller
{
    private function api(): AttendanceApiClient
    {
        return AttendanceApiClient::fromConfig();
    }

    private function token(): string
    {
        $token = (string) Session::get('api_token', '');
        if ($token === '') {
            throw new RuntimeException('Not authenticated');
        }

        return $token;
    }

    public function showLogin()
    {
        if (Session::has('api_token')) {
            return redirect()->route('dashboard');
        }

        return view('admin.login');
    }

    public function login(Request $request)
    {
        $data = $request->validate([
            'email' => ['required', 'email'],
            'password' => ['required', 'string'],
        ]);

        try {
            $result = $this->api()->login($data['email'], $data['password']);
            $roles = $result['user']['roles'] ?? [];
            if (! in_array('Admin', $roles, true) && ! in_array('Manager', $roles, true)) {
                return back()->withErrors(['email' => 'Admin or Manager role required.'])->withInput();
            }

            Session::put('api_token', $result['token']);
            Session::put('api_user', $result['user']);

            return redirect()->route('dashboard');
        } catch (RuntimeException $e) {
            return back()->withErrors(['email' => $e->getMessage()])->withInput();
        }
    }

    public function logout()
    {
        Session::flush();

        return redirect()->route('login');
    }

    public function dashboard()
    {
        $user = Session::get('api_user');

        return view('admin.dashboard', compact('user'));
    }

    public function employees()
    {
        try {
            $employees = $this->api()->employees($this->token());
            $sites = $this->api()->sites($this->token());
        } catch (RuntimeException $e) {
            return back()->withErrors(['api' => $e->getMessage()]);
        }

        return view('admin.employees', compact('employees', 'sites'));
    }

    public function storeEmployee(Request $request)
    {
        $data = $request->validate([
            'employeeCode' => ['required', 'string', 'max:50'],
            'fullName' => ['required', 'string', 'max:200'],
            'department' => ['nullable', 'string', 'max:200'],
            'siteId' => ['nullable', 'uuid'],
        ]);

        try {
            $this->api()->createEmployee($this->token(), [
                'employeeCode' => $data['employeeCode'],
                'fullName' => $data['fullName'],
                'department' => $data['department'] ?? null,
                'siteId' => $data['siteId'] ?: null,
                'isActive' => true,
            ]);
        } catch (RuntimeException $e) {
            return back()->withErrors(['api' => $e->getMessage()])->withInput();
        }

        return redirect()->route('employees')->with('status', 'Employee created.');
    }

    public function sites()
    {
        try {
            $sites = $this->api()->sites($this->token());
        } catch (RuntimeException $e) {
            return back()->withErrors(['api' => $e->getMessage()]);
        }

        return view('admin.sites', compact('sites'));
    }

    public function storeSite(Request $request)
    {
        $data = $request->validate([
            'name' => ['required', 'string', 'max:200'],
            'timezone' => ['nullable', 'string', 'max:100'],
            'allowedCidrs' => ['nullable', 'string', 'max:1000'],
        ]);

        try {
            $this->api()->createSite($this->token(), [
                'name' => $data['name'],
                'timezone' => $data['timezone'] ?? null,
                'allowedCidrs' => $data['allowedCidrs'] ?? null,
                'isActive' => true,
            ]);
        } catch (RuntimeException $e) {
            return back()->withErrors(['api' => $e->getMessage()])->withInput();
        }

        return redirect()->route('sites')->with('status', 'Site created.');
    }

    public function users()
    {
        try {
            $users = $this->api()->users($this->token());
        } catch (RuntimeException $e) {
            return back()->withErrors(['api' => $e->getMessage()]);
        }

        return view('admin.users', compact('users'));
    }

    public function manualForm()
    {
        try {
            $employees = $this->api()->employees($this->token());
            $sites = $this->api()->sites($this->token());
        } catch (RuntimeException $e) {
            return back()->withErrors(['api' => $e->getMessage()]);
        }

        return view('admin.manual', compact('employees', 'sites'));
    }

    public function storeManual(Request $request)
    {
        $data = $request->validate([
            'employeeId' => ['required', 'uuid'],
            'eventType' => ['required', 'in:In,Out'],
            'siteId' => ['nullable', 'uuid'],
            'note' => ['nullable', 'string', 'max:500'],
        ]);

        try {
            $this->api()->manualAttendance($this->token(), [
                'employeeId' => $data['employeeId'],
                'eventType' => $data['eventType'],
                'siteId' => $data['siteId'] ?: null,
                'note' => $data['note'] ?? 'Manual correction',
                'idempotencyKey' => (string) str()->uuid(),
            ]);
        } catch (RuntimeException $e) {
            return back()->withErrors(['api' => $e->getMessage()])->withInput();
        }

        return redirect()->route('manual')->with('status', 'Manual attendance recorded via API.');
    }

    public function reports(Request $request)
    {
        try {
            $employees = $this->api()->employees($this->token());
        } catch (RuntimeException $e) {
            return back()->withErrors(['api' => $e->getMessage()]);
        }

        $employeeId = $request->query('employeeId');
        $year = (int) $request->query('year', now()->year);
        $month = (int) $request->query('month', now()->month);
        $report = null;

        if ($employeeId) {
            try {
                $report = $this->api()->monthlyReport($this->token(), $employeeId, $year, $month);
            } catch (RuntimeException $e) {
                return back()->withErrors(['api' => $e->getMessage()]);
            }
        }

        return view('admin.reports', compact('employees', 'employeeId', 'year', 'month', 'report'));
    }

    public function export(Request $request): StreamedResponse
    {
        $data = $request->validate([
            'employeeId' => ['required', 'uuid'],
            'year' => ['required', 'integer'],
            'month' => ['required', 'integer', 'min:1', 'max:12'],
            'format' => ['required', 'in:xlsx,pdf'],
        ]);

        $binary = $this->api()->exportBinary(
            $this->token(),
            $data['employeeId'],
            (int) $data['year'],
            (int) $data['month'],
            $data['format']
        );

        $filename = sprintf('attendance_%d_%02d.%s', $data['year'], $data['month'], $data['format'] === 'pdf' ? 'pdf' : 'xlsx');
        $contentType = $data['format'] === 'pdf'
            ? 'application/pdf'
            : 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

        return response()->streamDownload(function () use ($binary) {
            echo $binary;
        }, $filename, ['Content-Type' => $contentType]);
    }
}
