@extends('layouts.app')
@section('title', 'Sites')
@section('content')
<div class="grid">
    <div class="panel">
        <h2>Sites</h2>
        <table>
            <thead><tr><th>Name</th><th>Timezone</th><th>CIDRs</th><th>Active</th></tr></thead>
            <tbody>
            @foreach($sites as $s)
                <tr>
                    <td>{{ $s['name'] ?? '' }}</td>
                    <td>{{ $s['timezone'] ?? '' }}</td>
                    <td>{{ $s['allowedCidrs'] ?? '—' }}</td>
                    <td>{{ ($s['isActive'] ?? false) ? 'Yes' : 'No' }}</td>
                </tr>
            @endforeach
            </tbody>
        </table>
    </div>
    <div class="panel">
        <h2>Add site</h2>
        <form method="post" action="{{ route('sites.store') }}">
            @csrf
            <label>Name <input name="name" required></label>
            <label>Timezone <input name="timezone" placeholder="Asia/Yerevan"></label>
            <label>Allowed CIDRs <input name="allowedCidrs" placeholder="192.168.1.0/24"></label>
            <button type="submit">Create via API</button>
        </form>
    </div>
</div>
@endsection
