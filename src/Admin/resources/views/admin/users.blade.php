@extends('layouts.app')
@section('title', 'Users')
@section('content')
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
@endsection
