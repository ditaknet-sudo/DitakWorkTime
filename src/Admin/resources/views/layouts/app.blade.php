<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>@yield('title', 'Admin') — Ditak WorkTime</title>
    <style>
        :root { --bg:#eef2f6; --panel:#fff; --text:#15202b; --line:#c9d4df; --accent:#0f6a6a; --muted:#5b6b7c; --err:#b42318; }
        * { box-sizing: border-box; }
        body { margin:0; font-family:Segoe UI,Noto Sans,sans-serif; background:linear-gradient(160deg,#d9e6f2,#eef2f6 50%,#f7f3ea); color:var(--text); }
        .wrap { max-width:1100px; margin:0 auto; padding:1.25rem; }
        .top { display:flex; flex-wrap:wrap; gap:1rem; justify-content:space-between; align-items:center; margin-bottom:1rem; }
        .brand { font-weight:700; font-size:1.2rem; }
        nav a { margin-right:.9rem; color:inherit; text-decoration:none; opacity:.85; }
        nav a:hover { opacity:1; }
        .panel { background:var(--panel); border:1px solid var(--line); border-radius:16px; padding:1.25rem; box-shadow:0 12px 30px rgba(21,32,43,.08); }
        label { display:grid; gap:.35rem; margin-bottom:.75rem; }
        input, select, button, textarea { font:inherit; padding:.55rem .7rem; border-radius:8px; border:1px solid var(--line); }
        button { background:var(--accent); color:#fff; border:none; cursor:pointer; font-weight:600; }
        table { width:100%; border-collapse:collapse; margin-top:1rem; }
        th, td { text-align:left; padding:.55rem .35rem; border-bottom:1px solid var(--line); }
        .muted { color:var(--muted); }
        .error { color:var(--err); }
        .status { color:var(--accent); margin-bottom:.75rem; }
        .grid { display:grid; gap:1rem; grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); }
        .actions { display:flex; gap:.5rem; flex-wrap:wrap; align-items:end; }
    </style>
</head>
<body>
<div class="wrap">
    @if(session()->has('api_token'))
        <div class="top">
            <div class="brand">Ditak WorkTime Admin</div>
            <nav>
                <a href="{{ route('dashboard') }}">Dashboard</a>
                <a href="{{ route('employees') }}">Employees</a>
                <a href="{{ route('sites') }}">Sites</a>
                <a href="{{ route('users') }}">Users</a>
                <a href="{{ route('manual') }}">Manual</a>
                <a href="{{ route('reports') }}">Reports</a>
            </nav>
            <form method="post" action="{{ route('logout') }}">
                @csrf
                <button type="submit">Logout</button>
            </form>
        </div>
    @endif

    @if(session('status'))
        <div class="status">{{ session('status') }}</div>
    @endif
    @if($errors->any())
        <div class="error panel" style="margin-bottom:1rem;">
            @foreach($errors->all() as $error)
                <div>{{ $error }}</div>
            @endforeach
        </div>
    @endif

    @yield('content')
</div>
</body>
</html>
