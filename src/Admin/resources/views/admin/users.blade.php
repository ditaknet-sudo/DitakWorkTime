@extends('layouts.app')
@section('title', 'Users')
@section('content')
<div class="grid">
    <div class="panel">
        <h2>Users & roles</h2>
        <table>
            <thead><tr><th>Email</th><th>Name</th><th>Roles</th><th>Active</th></tr></thead>
            <tbody>
            @foreach($users as $u)
                <tr>
                    <td>{{ $u['email'] ?? '' }}</td>
                    <td>{{ $u['displayName'] ?? '' }}</td>
                    <td>{{ implode(', ', $u['roles'] ?? []) }}</td>
                    <td>{{ ($u['isActive'] ?? false) ? 'Yes' : 'No' }}</td>
                </tr>
            @endforeach
            </tbody>
        </table>
    </div>
    <div class="panel">
        <h2>Add user</h2>
        <form method="post" action="{{ route('users.store') }}">
            @csrf
            <label>Email <input type="email" name="email" value="{{ old('email') }}" required></label>
            <label>Display name <input name="displayName" value="{{ old('displayName') }}" required></label>
            <label>Password <input type="password" name="password" minlength="12" required></label>
            <label>Confirm password <input type="password" name="password_confirmation" minlength="12" required></label>
            <fieldset>
                <legend>Roles</legend>
                @foreach(['Admin', 'Manager', 'Director', 'Accountant', 'Employee'] as $role)
                    <label><span><input type="checkbox" name="roles[]" value="{{ $role }}" @checked(in_array($role, old('roles', []), true))> {{ $role }}</span></label>
                @endforeach
            </fieldset>
            <label>Linked employee
                <select name="employeeId">
                    <option value="">None</option>
                    @foreach($employees as $employee)
                        <option value="{{ $employee['id'] }}" @selected(old('employeeId') === ($employee['id'] ?? null))>
                            {{ $employee['fullName'] }} ({{ $employee['employeeCode'] }})
                        </option>
                    @endforeach
                </select>
            </label>
            <button type="submit">Create user</button>
        </form>
    </div>
</div>
@endsection
