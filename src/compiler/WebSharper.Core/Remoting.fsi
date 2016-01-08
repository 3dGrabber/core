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

/// Implements server-side remote procedure call support.
module WebSharper.Core.Remoting

module A = WebSharper.Core.Attributes
module M = WebSharper.Core.Metadata
//module R = WebSharper.Core.Reflection

/// Represents the response.
type Response =
    {
        Content : string
        ContentType : string
    }

/// Represents read-only access to HTTP headers.
type Headers = string -> option<string>

/// Represents an incoming request.
type Request =
    {
        Body : string
        Headers : Headers
    }

/// Tests if the given request is marked as a
/// WebSharper remote procedure call request.
val IsRemotingRequest : Headers -> bool

/// Constructs RPC handlers.
type IHandlerFactory =

    /// Creates a new handler based on its type.
    abstract member Create : System.Type -> option<obj>

/// Sets the default RPC handler factory.
val SetHandlerFactory : IHandlerFactory -> unit

/// Handles remote procedure call requests.
[<Sealed>]
type Server =

    /// Creates a new instance.
    static member Create : option<IHandlerFactory> -> M.Info -> Server

    /// Handles a request.
    member HandleRequest : Request -> Async<Response>

    /// Exposes the Json encoding/decoding provider
    member JsonProvider : Json.Provider
