@extends('layouts.app')
@section('title', 'Reports')
@section('content')
<div class="panel">
    <h2>Reports</h2>
    <form method="get" action="{{ route('reports') }}" class="actions">
        <label>Employee
            <select name="employeeId" required>
                <option value="">Select…</option>
                @foreach($employees as $e)
                    <option value="{{ $e['id'] }}" @selected($employeeId === ($e['id'] ?? null))>{{ $e['fullName'] }}</option>
                @endforeach
            </select>
        </label>
        <label>Year <input type="number" name="year" value="{{ $year }}"></label>
        <label>Month <input type="number" name="month" min="1" max="12" value="{{ $month }}"></label>
        <button type="submit">Load</button>
    </form>

    @if($report)
        <p>Total minutes: <strong>{{ $report['totalMinutes'] ?? 0 }}</strong></p>
        <div class="actions">
            <a href="{{ route('reports.export', ['employeeId'=>$employeeId,'year'=>$year,'month'=>$month,'format'=>'xlsx']) }}">
                <button type="button">Export Excel</button>
            </a>
            <a href="{{ route('reports.export', ['employeeId'=>$employeeId,'year'=>$year,'month'=>$month,'format'=>'pdf']) }}">
                <button type="button">Export PDF</button>
            </a>
        </div>
        <table>
            <thead><tr><th>Date</th><th>Hours</th><th>Open</th></tr></thead>
            <tbody>
            @foreach(($report['days'] ?? []) as $d)
                <tr>
                    <td>{{ $d['workDate'] ?? '' }}</td>
                    <td>{{ $d['workedHoursDisplay'] ?? '' }}</td>
                    <td>{{ ($d['hasOpenShift'] ?? false) ? 'Yes' : 'No' }}</td>
                </tr>
            @endforeach
            </tbody>
        </table>
    @endif
</div>
@endsection
