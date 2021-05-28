﻿[<AutoOpen>]
module Farmer.Builders.TrafficManager

open Farmer
open Farmer.TrafficManager
open Farmer.Arm.TrafficManager
open System

type EndpointConfig =
    { Name : ResourceName
      Status : FeatureFlag
      Target : EndpointTarget
      Weight : int
      Priority : int }
    interface IBuilder with
        member this.ResourceId = azureEndpoints.resourceId this.Name
        member this.BuildResources location = [
            { Name = this.Name
              Status = this.Status
              Target = this.Target
              Weight = this.Weight
              Priority = this.Priority
              Location = match this.Target with
                         | External (_, l) -> Some l
                         |_ -> None
              }
        ]

type TrafficManagerConfig =
    { Name : ResourceName
      DnsTtl : int<Seconds>
      Status : FeatureFlag
      RoutingMethod : RoutingMethod
      MonitorConfig : MonitorConfig
      Endpoints : EndpointConfig list
      TrafficViewEnrollmentStatus : FeatureFlag
      Tags: Map<string,string> }
    interface IBuilder with
        member this.ResourceId = profiles.resourceId this.Name
        member this.BuildResources location = [
            { Name = this.Name
              Status = this.Status
              RoutingMethod = this.RoutingMethod
              DnsTtl = this.DnsTtl
              MonitorConfig = this.MonitorConfig
              TrafficViewEnrollmentStatus = this.TrafficViewEnrollmentStatus
              Dependencies = Set.empty
              Tags = this.Tags
              Endpoints = this.Endpoints
                          |> List.map (fun e -> { Name = e.Name
                                                  Status = e.Status
                                                  Target = e.Target
                                                  Weight = e.Weight
                                                  Priority = e.Priority
                                                  Location = match e.Target with
                                                             | External (_, l) -> Some l
                                                             | _ -> None })}
        ]

type EndpointBuilder() =
    member __.Yield _ =
        { Name = ResourceName.Empty
          Status = Enabled
          Target = EndpointTarget.Website ResourceName.Empty
          Weight = 1
          Priority = 1 }

    member __.Run (state:EndpointConfig) =
        state

    /// Sets the name of the Endpoint
    [<CustomOperation "name">]
    member __.Name(state:EndpointConfig, name) = { state with Name = name }
    member this.Name(state:EndpointConfig, name) = this.Name(state, ResourceName name)

    /// Sets the weight of the Endpoint
    [<CustomOperation "weight">]
    member __.Weight(state:EndpointConfig, weight) = { state with Weight = weight }

    /// Disables the Endpoint
    [<CustomOperation "disable_endpoint">]
    member __.DisableEndpoint(state:EndpointConfig) = { state with Status = Disabled }

    /// Enables the Endpoint
    [<CustomOperation "enable_endpoint">]
    member __.EnableEndpoint(state:EndpointConfig) = { state with Status = Enabled }

    /// Sets the priority of the Endpoint
    [<CustomOperation "priority">]
    member __.Priority(state:EndpointConfig, priority) = { state with Priority = priority }

    /// Sets the target of the Endpoint to a web app
    [<CustomOperation "target_webapp">]
    member __.TargetWebSite(state:EndpointConfig, name) = { state with Target = Website name }
    member __.TargetWebSite(state:EndpointConfig, (webApp: WebAppConfig)) = { state with Target = Website webApp.Name }

    /// Sets the target of the Endpoint to an external domain and location
    [<CustomOperation "target_external">]
    member __.TargetExternal(state:EndpointConfig, domain, location) =
        { state with Target = External (domain, location) }

type TrafficManagerBuilder() =
    member __.Yield _ =
        { Name = ResourceName.Empty
          DnsTtl = 30<Seconds>
          Status = Enabled
          RoutingMethod = RoutingMethod.Performance
          TrafficViewEnrollmentStatus = Disabled
          Endpoints = []
          MonitorConfig =
            { Protocol = MonitorProtocol.Https
              Port = 443
              Path = "/"
              IntervalInSeconds = 30<Seconds>
              ToleratedNumberOfFailures = 3
              TimeoutInSeconds = 10<Seconds> }
          Tags = Map.empty }

    member __.Run (state:TrafficManagerConfig) =
        state

    /// Sets the name of the Traffic Manager profile
    [<CustomOperation "name">]
    member __.Name(state:TrafficManagerConfig, name) = { state with Name = name }
    member this.Name(state:TrafficManagerConfig, name) = this.Name(state, ResourceName name)

    /// Adds tags to the Traffic Manager profile
    [<CustomOperation "add_tags">]
    member _.Tags(state:TrafficManagerConfig, pairs) =
        { state with
            Tags = pairs |> List.fold (fun map (key,value) -> Map.add key value map) state.Tags }

    /// Adds a tag to the Traffic Manager profile
    [<CustomOperation "add_tag">]
    member this.Tag(state:TrafficManagerConfig, key, value) = this.Tags(state, [ (key,value) ])

    /// Adds Endpoints to the Traffic Manager profile
    [<CustomOperation "add_endpoints">]
    member _.AddEndpoints(state:TrafficManagerConfig, endpoints:EndpointConfig list) =
        { state with Endpoints = state.Endpoints @ endpoints }
    member this.AddEndpoints(state:TrafficManagerConfig, endpoint:EndpointConfig) =
        this.AddEndpoints(state, [endpoint])

    /// Sets the DNS TTL of the Traffic Manager profile, in seconds (default 30)
    [<CustomOperation "dns_ttl">]
    member __.DnsTtl(state:TrafficManagerConfig, ttl: int<Seconds>) = { state with DnsTtl = ttl }
    member this.DnsTtl(state:TrafficManagerConfig, ttl: TimeSpan) = { state with DnsTtl = (int ttl.TotalSeconds) * 1<Seconds> }

    /// Disables the Traffic Manager profile
    [<CustomOperation "disable_profile">]
    member __.DisableProfileStatus(state:TrafficManagerConfig) = { state with Status = Disabled }

    /// Enables the Traffic Manager profile
    [<CustomOperation "enable_profile">]
    member __.EnableProfileStatus(state:TrafficManagerConfig) = { state with Status = Enabled }

    /// Sets the routing method of the Traffic Manager profile (default Performance)
    [<CustomOperation "routing_method">]
    member __.RoutingMethod(state:TrafficManagerConfig, routingMethod) = { state with RoutingMethod = routingMethod }

    /// Enables the Traffic View of the Traffic Manager profile
    [<CustomOperation "enable_traffic_view">]
    member __.EnableTrafficView(state:TrafficManagerConfig) =
        { state with TrafficViewEnrollmentStatus = Enabled }

    /// Disables the Traffic View of the Traffic Manager profile
    [<CustomOperation "disable_traffic_view">]
    member __.DisableTrafficView(state:TrafficManagerConfig) =
        { state with TrafficViewEnrollmentStatus = Disabled }

    /// Sets the monitoring protocol of the Traffic Manager profile (default Https)
    [<CustomOperation "monitor_protocol">]
    member __.MonitorProtocol(state:TrafficManagerConfig, protocol) =
        { state with MonitorConfig = { state.MonitorConfig with Protocol = protocol } }

    /// Sets the monitoring port of the Traffic Manager profile (default 443)
    [<CustomOperation "monitor_port">]
    member __.MonitorPort(state:TrafficManagerConfig, port) =
        { state with MonitorConfig = { state.MonitorConfig with Port = port } }

    /// Sets the monitoring path of the Traffic Manager profile (default /)
    [<CustomOperation "monitor_path">]
    member __.MonitorPath(state:TrafficManagerConfig, path) =
        { state with MonitorConfig = { state.MonitorConfig with Path = path } }

    /// Sets the monitoring interval, in seconds, of the Traffic Manager profile (default 30)
    [<CustomOperation "monitor_interval">]
    member __.MonitorInterval(state:TrafficManagerConfig, interval) =
        { state with MonitorConfig = { state.MonitorConfig with IntervalInSeconds = interval } }
    member this.MonitorInterval(state:TrafficManagerConfig, interval: TimeSpan) =
        this.MonitorInterval(state, (int interval.TotalSeconds) * 1<Seconds>)

    /// Sets the monitoring timeout, in seconds, of the Traffic Manager profile (default 10)
    [<CustomOperation "monitor_timeout">]
    member __.MonitorTimeout(state:TrafficManagerConfig, timeout) =
        { state with MonitorConfig = { state.MonitorConfig with TimeoutInSeconds = timeout } }
    member this.MonitorTimeout(state:TrafficManagerConfig, timeout: TimeSpan) =
        this.MonitorTimeout(state, (int timeout.TotalSeconds) * 1<Seconds>)

    /// Sets the monitoring tolerated number of failures, of the Traffic Manager profile (default 3)
    [<CustomOperation "monitor_tolerated_failures">]
    member __.MonitorToleratedFailures(state:TrafficManagerConfig, failures) =
        { state with MonitorConfig = { state.MonitorConfig with ToleratedNumberOfFailures = failures } }

// Expose Builders
let endpoint = EndpointBuilder()
let trafficManager = TrafficManagerBuilder()