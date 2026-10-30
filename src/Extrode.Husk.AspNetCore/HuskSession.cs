using Microsoft.Extensions.Logging;

namespace Extrode.Husk.AspNetCore;

/// <summary>What both Husk compilers share in the running app: the development map and the loaded
/// compiler.</summary>
internal sealed class HuskSession(string mapPath)
{
    private readonly Lock _gate = new();
    private DateTime _mapStamp;
    private HuskDevelopmentMap? _map;
    private CompilerBridge? _bridge;
    private string? _bridgePath;

    // The map is read again whenever a build rewrites it, so a build while the app runs is picked up.
    // The compiler stays loaded: a load context cannot drop V8, so a new compiler path needs a restart.
    public (HuskDevelopmentMap Map, CompilerBridge Bridge)? Load(ILogger logger)
    {
        lock (_gate)
        {
            var stamp = File.GetLastWriteTimeUtc(mapPath);
            if (stamp != _mapStamp)
            {
                _map = null;
                try
                {
                    _map = HuskDevelopmentMap.Read(mapPath);
                    if (_map is null)
                        logger.LogError("{Path} is missing; build the project again to compile on save", mapPath);
                    else if (_bridge is null)
                        _bridge = CompilerBridge.Load(_map.Compiler, logger);
                    else if (_bridgePath != _map.Compiler)
                        logger.LogWarning("Husk's compiler moved to {Path}; restart the app to compile saves with it", _map.Compiler);
                    if (_bridge is not null && _map is not null)
                    {
                        _bridgePath ??= _map.Compiler;
                        // Remembered only once the map and compiler loaded: a half-written map that is
                        // completed within the same timestamp is read again on the next save.
                        _mapStamp = stamp;
                    }
                }
                catch (Exception e)
                {
                    _map = null;
                    logger.LogError(e, "Husk could not load its compiler from {Path}; build the project again to compile on save", mapPath);
                }
            }
            else if (_map is null || _bridge is null)
                logger.LogError("Husk is not compiling saves (see the error above); build the project again to compile on save");
            return _map is not null && _bridge is not null ? (_map, _bridge) : null;
        }
    }

}
