using System.Text;
using Prospect.Unreal.Core;
using Prospect.Unreal.Core.Names;
using Prospect.Unreal.Exceptions;
using Prospect.Unreal.Net;
using Prospect.Unreal.Net.Actors;
using Prospect.Unreal.Net.Channels;
using Prospect.Unreal.Net.Packets.Bunch;
using Prospect.Unreal.Net.Packets.Control;
using Serilog;

namespace Prospect.Unreal.Runtime;

// do both PendingNetGame and World in here maybe?

public class NetGameWorld : FNetworkNotify, IAsyncDisposable
{
	private static readonly ILogger Logger = Log.ForContext<NetGameWorld>();
	UNetConnection Connection;
	public UIpNetDriver Connect(FUrl worldUrl)
	{
		var connection = new UIpNetDriver(worldUrl.Host, worldUrl.Port, false);
		connection.InitConnect(this, worldUrl);
		Connection = connection.ServerConnection;
		connection.ServerConnection.Handler?.BeginHandshaking(SendInitialJoin);
		return connection;
	}
	public void Tick(float TickRate)
	{
		if (Connection != null)
		{
			Connection.Driver?.TickDispatch(TickRate);
			Connection.Driver?.PostTickDispatch();

			Connection.Driver?.TickFlush(TickRate);
			Connection.Driver?.PostTickFlush();
		}
	}
	public void SendInitialJoin()
	{
		NMT_Hello.Send(Connection, 1, FNetworkVersion.GetLocalNetworkVersion(), "");
		Connection.FlushNet();
	}

	void FNetworkNotify.NotifyAcceptedConnection(UNetConnection connection)
	{
	}

	bool FNetworkNotify.NotifyAcceptingChannel(UChannel channel)
	{
		return true;
	}

	EAcceptConnection FNetworkNotify.NotifyAcceptingConnection()
	{
		throw new NotImplementedException();
	}

	void FNetworkNotify.NotifyControlMessage(UNetConnection connection, NMT messageType, FInBunch bunch)
	{
		switch (messageType)
		{
			case NMT.Upgrade:
				// Report mismatch.
				if (NMT_Upgrade.Receive(bunch, out uint remoteNetworkVersion))
				{
					// Upgrade
					//"Engine", "ClientOutdated", "The match you are trying to join is running an incompatible version of the game.  Please try upgrading your game version.";
				}
				break;
			case NMT.Failure:
				// our connection attempt failed for some reason, for example a synchronization mismatch (bad GUID, etc) or because the server rejected our join attempt (too many players, etc)
				// here we can further parse the string to determine the reason that the server closed our connection and present it to the user
				if (NMT_Failure.Receive(bunch, out string errorMsg))
				{
				}
				break;
			case NMT.Challenge:
				// Challenged by server.string ErrorMsg;
				if (NMT_Challenge.Receive(bunch, out string challenge))
				{

					/*ULocalPlayer* LocalPlayer = GEngine->GetFirstGamePlayer(this);
					if (LocalPlayer)
					{
						// Send the player nickname if available
						FString OverrideName = LocalPlayer->GetNickname();
						if (OverrideName.Len() > 0)
						{
							PartialURL.AddOption(*FString::Printf(TEXT("Name=%s"), *OverrideName));
						}

						// Send any game-specific url options for this player
						FString GameUrlOptions = LocalPlayer->GetGameLoginOptions();
						if (GameUrlOptions.Len() > 0)
						{
							PartialURL.AddOption(*FString::Printf(TEXT("%s"), *GameUrlOptions));
						}

						// Send the player unique Id at login
						Connection->PlayerId = LocalPlayer->GetPreferredUniqueNetId();
					}

					// Send the player's online platform name
					FName OnlinePlatformName = NAME_None;
					if (const FWorldContext* const WorldContext = GEngine->GetWorldContextFromPendingNetGame(this))
			{
						if (WorldContext->OwningGameInstance)
						{
							OnlinePlatformName = WorldContext->OwningGameInstance->GetOnlinePlatformName();
						}
					}

					Connection->ClientResponse = TEXT("0");
					FString URLString(PartialURL.ToString());
					FString OnlinePlatformNameString = OnlinePlatformName.ToString();*/
					
					NMT_Login.Send(connection, "0", "?UserName=" + Guid.NewGuid().ToString().Substring(0, 5)  + "?Name=DESKTOP-D9VF075-8D7A15FD4638B27FE48A0A919C7" + Guid.NewGuid().ToString().Substring(0, 5), new FUniqueNetIdRepl(), "NULL");
					//NetDriver->ServerConnection->FlushNet();
				}
				break;
			case NMT.Welcome:
				if (NMT_Welcome.Receive(bunch, out string levelName, out string gameName, out string redirectURL))
				{
					// Server accepted connection.
					//UE_LOG(LogNet, Log, TEXT("Welcomed by server (Level: %s, Game: %s)"), *URL.Map, *GameName);

					// extract map name and options
					//if (GameName.Len() > 0) URL.AddOption(*FString::Printf(TEXT("game=%s"), *GameName));
					// Send out netspeed now that we're connected
					NMT_Netspeed.Send(connection, connection.CurrentNetSpeed);
					connection.FlushNet();
					// We have successfully connected
					// TickWorldTravel will load the map and call LoadMapCompleted which eventually calls SendJoin
					//bSuccessfullyConnected = true;
					NMT_Join.Send(connection);
					connection.FlushNet();
				}

				break;
			/*case NMT_NetGUIDAssign:
				{
					FNetworkGUID NetGUID;
					FString Path;

					if (FNetControlMessage < NMT_NetGUIDAssign >::Receive(Bunch, NetGUID, Path))
					{
						NetDriver->ServerConnection->PackageMap->ResolvePathAndAssignNetGUID(NetGUID, Path);
					}

					break;
				}
			case NMT_EncryptionAck:
				{
					if (FNetDelegates::OnReceivedNetworkEncryptionAck.IsBound())
					{
						TWeakObjectPtr<UNetConnection> WeakConnection = Connection;
						FNetDelegates::OnReceivedNetworkEncryptionAck.Execute(FOnEncryptionKeyResponse::CreateUObject(this, &UPendingNetGame::FinalizeEncryptedConnection, WeakConnection));
					}
					else
					{
						// This error will be resolved in TickWorldTravel()
						ConnectionError = TEXT("No encryption ack handler");

						// Force close the session
						UE_LOG(LogNet, Warning, TEXT("%s: No delegate available to handle encryption ack, disconnecting."), *Connection->GetName());
						Connection->Close();
					}
					break;
				}*/
			default:
				Logger.Error(" --- Unknown/unexpected message for pending level");
				break;
		}
	}
	public async ValueTask DisposeAsync()
	{
		//if (NetDriver != null)
		{
			//await NetDriver.DisposeAsync();
		}
	}
}
