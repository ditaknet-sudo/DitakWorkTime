@extends('layouts.app')
@section('title', 'Dashboard')
@section('content')
<div class="panel">
    <h1>Control Panel</h1>
    <p>Signed in as <strong>{{ $user['displayName'] ?? $user['email'] ?? 'user' }}</strong></p>
    <p class="muted">Roles: {{ implode(', ', $user['roles'] ?? []) }}</p>
    <p>All employee/site/attendance changes go through the .NET Web API Core.</p>
</div>
@endsection
