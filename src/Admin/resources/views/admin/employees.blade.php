@extends('layouts.app')
@section('title', 'Employees')
@section('content')
<div class="grid">
    <div class="panel">
        <h2>Employees</h2>
        <table>
            <thead><tr><th>Code</th><th>Name</th><th>Department</th><th>Active</th></tr></thead>
            <tbody>
            @foreach($employees as $e)
                <tr>
                    <td>{{ $e['employeeCode'] ?? '' }}</td>
                    <td>{{ $e['fullName'] ?? '' }}</td>
                    <td>{{ $e['department'] ?? '—' }}</td>
                    <td>{{ ($e['isActive'] ?? false) ? 'Yes' : 'No' }}</td>
                </tr>
            @endforeach
            </tbody>
        </table>
    </div>
    @if(in_array('Admin', session('api_user.roles', []), true))
    <div class="panel">
        <h2>Add employee</h2>
        <form method="post" action="{{ route('employees.store') }}">
            @csrf
            <label>Code <input name="employeeCode" required></label>
            <label>Full name <input name="fullName" required></label>
            <label>Department <input name="department"></label>
            <label>Site
                <select name="siteId">
                    <option value="">—</option>
                    @foreach($sites as $s)
                        <option value="{{ $s['id'] }}">{{ $s['name'] }}</option>
                    @endforeach
                </select>
            </label>
            <button type="submit">Create via API</button>
        </form>
    </div>
    @endif
</div>
@endsection
