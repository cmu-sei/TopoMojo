# TopoMojo

A virtual lab builder and player

## Overview
TopoMojo is an ASP.NET CORE application for creating training content,
or "Labs", and publishing those labs so others can consume them.

"Topo" is short for "topology", as in a network topology of
computer hosts.  "Mojo" is the magic of deploying and accessing
those resources on demand.

Great for use in a classroom or training environment where hands-
on skill building is desired.

Goals:
* Abstract the infrastructure concerns from content producers.
* One-click access for content consumers.

## Getting Started

Build with Docker, or:
1. Install .Net Core SDK 3.1
2. `dotnet run`
3. Browse to `http://localhost:5000/api`

*NOTE*: The separate `topomojo-ui` repository holds the user interface
for this api.

By default, TopoMojo starts with a "Mock" hypervisor manager so you
can investigate it without having to connect it to your hypervisors.
When ready for that you'll need to edit the "Pod" fields in `appsettings.conf`, (or rather in an `appsettings.Development.conf`
copy of it.)

*TODO:* More info

It also starts with an in-memory database; not usable for production,
but nice for quick startup.  Use the appsettings file to switch to
PostgreSQL or SqlServer.

The end of `appsettings.conf` has an example `appsettings.Development.conf`.

## Documentation

TopoMojo is part of the [Crucible](https://cmu-sei.github.io/crucible/) framework. We maintain comprehensive documentation for all Crucible apps, including Gameboard, there. [Check it out!](https://cmu-sei.github.io/crucible/topomojo/about/)
