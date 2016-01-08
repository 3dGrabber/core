// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2015 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace WebSharper

open WebSharper.JavaScript

[<Proxy(typeof<double>)>]
type private DoubleProxy =

    [<Inline "Math.abs($0) === Infinity">]
    static member IsInfinity(f: double) = X<bool>

    [<Inline "isNaN($0)">]
    static member IsNaN(f: double) = X<bool>

    [<Inline "$0 === -Infinity">]
    static member IsNegativeInfinity (f: double) = X<bool>

    [<Inline "$0 === Infinity">]
    static member IsPositiveInfinity (f: double) = X<bool>

    [<Inline "parseFloat($0)">]
    static member Parse(s: string) = X<double>

    [<Inline "$a + $b">]
    static member (+) (a: double, b: double) = X<double>

    [<Inline "$a - $b">]
    static member (-) (a: double, b: double) = X<double>

    [<Inline "$a * $b">]
    static member (*) (a: double, b: double) = X<double>

    [<Inline "$a / $b">]
    static member (/) (a: double, b: double) = X<double>

    [<Inline "$a % $b">]
    static member (%) (a: double, b: double) = X<double>

    [<Inline "-$a">]
    static member (~-) (a: double) = X<double>

    [<Inline "+$a">]
    static member (~+) (a: double) = X<double>

    [<Inline "$a == $b">]
    static member op_Equality (a: double, b: double) = X<bool>

    [<Inline "$a != $b">]
    static member op_Inequality (a: double, b: double) = X<bool>

    [<Inline "$a > $b">]
    static member op_GreaterThan (a: double, b: double) = X<bool>

    [<Inline "$a >= $b">]
    static member op_GreaterThanOrEqual (a: double, b: double) = X<bool>

    [<Inline "$a < $b">]
    static member op_LessThan (a: double, b: double) = X<bool>

    [<Inline "$a <= $b">]
    static member op_LessThanOrEqual (a: double, b: double) = X<bool>

    static member MaxValue
        with [<Inline "Number.MAX_VALUE">] get () = X<double>

    static member MinValue
        with [<Inline "Number.MIN_VALUE">] get () = X<double>
