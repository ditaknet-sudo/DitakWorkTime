@extends('layouts.app')
@section('title', 'Manual')
@section('content')
<div class="panel" style="max-width:560px;">
    <h2>Manual attendance correction</h2>
    <p class="muted">Writes <code>source=manual</code> through the Web API only.</p>
    <form method="post" action="{{ route('manual.store') }}">
        @csrf
        <label>Employee
            <select name="employeeId" required>
                @foreach($employees as $e)
                    <option value="{{ $e['id'] }}">{{ $e['fullName'] }} ({{ $e['employeeCode'] }})</option>
                @endforeach
            </select>
        </label>
        <label>Event
            <select name="eventType">
                <option value="In">Check in</option>
                <option value="Out">Check out</option>
            </select>
        </label>
        <label>Site
            <select name="siteId">
                <option value="">—</option>
                @foreach($sites as $s)
                    <option value="{{ $s['id'] }}">{{ $s['name'] }}</option>
                @endforeach
            </select>
        </label>
        <label>Note <input name="note" value="Manual correction"></label>
        <button type="submit">Submit via API</button>
    </form>
</div>
@endsection
