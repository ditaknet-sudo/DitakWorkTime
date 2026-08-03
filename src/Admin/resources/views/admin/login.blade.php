@extends('layouts.app')
@section('title', 'Login')
@section('content')
<div class="panel" style="max-width:420px;margin:4rem auto;">
    <h1>Ditak WorkTime Admin</h1>
    <p class="muted">Uses the same Web API JWT. No direct DB writes.</p>
    <form method="post" action="{{ route('login.submit') }}">
        @csrf
        <label>Email
            <input type="email" name="email" value="{{ old('email', 'admin@company.local') }}" required placeholder="admin@company.local">
        </label>
        <label>Password
            <input type="password" name="password" required placeholder="••••••••">
        </label>
        <button type="submit">Sign in</button>
    </form>
</div>
@endsection
