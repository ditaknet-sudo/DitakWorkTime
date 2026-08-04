<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Session;
use Symfony\Component\HttpFoundation\Response;

class RequireApiRole
{
    public function handle(Request $request, Closure $next, string ...$allowedRoles): Response
    {
        $roles = Session::get('api_user.roles', []);
        if (! is_array($roles) || array_intersect($allowedRoles, $roles) === []) {
            abort(403, 'Your role does not allow this action.');
        }

        return $next($request);
    }
}
