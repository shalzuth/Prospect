using System.Security.Cryptography;

namespace Prospect.Unreal.Net;

public class FNetworkVersion
{
    private static bool bHasCachedNetworkChecksum;
    private static uint _cachedNetworkChecksum;
    public static uint GetLocalNetworkVersion()
	{
		if (bHasCachedNetworkChecksum)
		{
			return _cachedNetworkChecksum;
		}

		var NetworkCompatibleChangelist = 0;
		var EngineNetworkProtocolVersion = 17; // HISTORY_ENGINENETVERSION_LATEST
		var AppName = "FPS"; // or AppDomain.CurrentDomain.FriendlyName
		var Version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
		var versionString = $"{AppName} {Version}, NetCL: {NetworkCompatibleChangelist}, EngineNetVer: {EngineNetworkProtocolVersion}, GameNetVer: {0}";
		versionString = "FPS 1.0.0.0, NetCL: 0, EngineNetVer: 17, GameNetVer: 0";
		_cachedNetworkChecksum = BitConverter.ToUInt32(System.IO.Hashing.Crc32.Hash(System.Text.Encoding.UTF32.GetBytes(versionString.ToLower())));
		bHasCachedNetworkChecksum = true;

		return _cachedNetworkChecksum;
	}
}