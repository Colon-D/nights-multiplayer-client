using Reloaded.Mod.Interfaces;
using nights.test.client.Template;
using nights.test.client.Configuration;
using Reloaded.Hooks.Definitions;
using CallingConventions = Reloaded.Hooks.Definitions.X86.CallingConventions;
using Reloaded.Hooks.Definitions.X86;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;
using Reloaded.Memory.Sources;
using Reloaded.Imgui.Hook.Implementations;
using Reloaded.Imgui.Hook;
using DearImguiSharp;
using nights.test.client.structs;
using Reloaded.Hooks.Definitions.Enums;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace nights.test.client;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : ModBase // <= Do not Remove.
{
	/// <summary>
	/// Provides access to the mod loader API.
	/// </summary>
	private readonly IModLoader _modLoader;

	/// <summary>
	/// Provides access to the Reloaded.Hooks API.
	/// </summary>
	/// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
	private readonly IReloadedHooks _hooks;

	/// <summary>
	/// Provides access to the Reloaded logger.
	/// </summary>
	private readonly ILogger _logger;

	/// <summary>
	/// Entry point into the mod, instance that created this class.
	/// </summary>
	private readonly IMod _owner;

	/// <summary>
	/// Provides access to this mod's configuration.
	/// </summary>
	private Config _configuration;

	/// <summary>
	/// The configuration of the currently executing mod.
	/// </summary>
	private readonly IModConfig _modConfig;

	public static ImVec4 Color(uint hexColor) {
		byte red = (byte)((hexColor >> 24) & 0xFF);
		byte green = (byte)((hexColor >> 16) & 0xFF);
		byte blue = (byte)((hexColor >> 8) & 0xFF);
		byte alpha = (byte)(hexColor & 0xFF);

		float normalizedRed = red / 255.0f;
		float normalizedGreen = green / 255.0f;
		float normalizedBlue = blue / 255.0f;
		float normalizedAlpha = alpha / 255.0f;

		return new ImVec4 {
			X = normalizedRed,
			Y = normalizedGreen,
			Z = normalizedBlue,
			W = normalizedAlpha
		};
	}

	public Mod(ModContext context) {
		_modLoader = context.ModLoader;
		_hooks = context.Hooks;
		_logger = context.Logger;
		_owner = context.Owner;
		_configuration = context.Configuration;
		_modConfig = context.ModConfig;

		Globals.Hooks = _hooks;

		// initialize Dear ImGui
		SDK.Init(_hooks);
		const string DefaultIp = "localhost";
		Encoding.UTF8.GetBytes(DefaultIp, 0, DefaultIp.Length, _multiplayerHost, 0);
		ImguiHook.Create(Imgui, new ImguiHookOptions() {
			Implementations = new List<IImguiHook>() {
				new ImguiHookDx9()
			}
		}).ConfigureAwait(false);

		// don't write imgui file
		ImGui.GetIO().IniFilename = null;
		// PRESENTATION!
		var style = ImGui.GetStyle();
		var colors = style.Colors;
		colors[(int)ImGuiCol.WindowBg] = Color(0x8C00ADFF);
		colors[(int)ImGuiCol.Button] = Color(0xFF31B5FF);
		colors[(int)ImGuiCol.ButtonHovered] = Color(0xFF31B57F);
		colors[(int)ImGuiCol.ButtonActive] = Color(0xFF31B53F);
		colors[(int)ImGuiCol.CheckMark] = Color(0xFFFFFFFF);
		colors[(int)ImGuiCol.FrameBg] = Color(0xFF31B5FF);
		colors[(int)ImGuiCol.FrameBgHovered] = Color(0xFF31B57F);
		colors[(int)ImGuiCol.FrameBgActive] = Color(0xFF31B53F);
		colors[(int)ImGuiCol.Separator] = Color(0xFF31B5FF);
		colors[(int)ImGuiCol.TitleBg] = Color(0xFF31B5FF);
		colors[(int)ImGuiCol.TitleBgActive] = Color(0xFF31B5FF);
		colors[(int)ImGuiCol.TitleBgCollapsed] = Color(0xFF31B5FF);
		style.Colors = colors;
		style.WindowBorderSize = 0f;
		style.WindowRounding = 8f;

		unsafe {
			// jump past code that hides cursor
			const byte jmp_rel8 = 0xEB;
			Memory.Instance.SafeWrite(0x40A88F, jmp_rel8);

			//MainGameLoopHook = _hooks.CreateHook<MainGameLoop>(MainGameLoopImpl, 0x40a460).Activate();

			// write jmp to 0x40AE56 (allow multiple instances to run at the same time)
			Memory.Instance.SafeWrite(0x40AE56, jmp_rel8);
			// enable background input
			Memory.Instance.SafeWrite(0x40A850, (byte)1);
		}
	}

	private bool _imgui_open = true;
	private byte[] _multiplayerHost = new byte[255];
	private ushort _multiplayerPort = 46944;
	public bool disconnectFromServer = false;
	public bool connectingToServer = false;
	private void Imgui() {
		if (!ImGui.Begin(
			"NiGHTS Client Debugger",
			ref _imgui_open,
			(int)ImGuiWindowFlags.NoResize | (int)ImGuiWindowFlags.NoMove
		)) {
			return;
		}
		ImGui.SetWindowPosVec2(new ImVec2 { X = 8, Y = 8 }, 0);
		ImGui.SetWindowSizeVec2(new ImVec2 { X = 192 + 96, Y = 0 }, 0);

		unsafe {
			if (connectingToServer) {
				ImGui.Text("Connecting...");
			}
			else if (!Server.Connected) {
				ImGui.BeginColumns("IP Address", 2, 0);
				ImGui.SetColumnWidth(0, 192f);
				ImGui.SetColumnWidth(1, 96f);
				ImGui.SetNextItemWidth(192f);
				fixed (byte* buffer = _multiplayerHost) {
					ImGui.InputText("##Host", (sbyte*)buffer, 255, 0, null, 0);
				}
				ImGui.NextColumn();
				ImGui.SetNextItemWidth(96f);
				fixed (ushort* multiplayerPortPtr = &_multiplayerPort) {
					ImGui.InputScalar("##Port", (int)ImGuiDataType.U16, (nint)multiplayerPortPtr, 0, 0, "%i", 0);
				}
				ImGui.EndColumns();

				if ((*Globals.WorldManager)->Player == null) {
					ImGui.Text("Please enter a Dream...");
				}
				else {
					if (ImGui.Button("Connect", new ImVec2 { X = 0, Y = 0 })) {
						ConnectToServer();
					}
				}
			}
			else {
				if (ImGui.Button("Disconnect", new ImVec2 { X = 0, Y = 0 })) {
					disconnectFromServer = true;
				}
			}
			ImGui.Separator();
		}
	}

	[Function(new[] { Register.eax, Register.ecx, Register.esi }, Register.edi, StackCleanup.Caller)]
	public unsafe delegate int PlayerSubCtor(int visitor_subtype, nuint player, PlayerSub* visitor);

	public unsafe bool ConnectToServerOnce = false;

	public unsafe IAsmHook RenderPlayersAsmHook;
	[Function(CallingConventions.Cdecl)]
	public unsafe delegate void RenderPlayersReverseWrapper();
	public IReverseWrapper<RenderPlayersReverseWrapper> renderPlayersReverseWrapper;
	[Function(CallingConventions.MicrosoftThiscall)]
	public unsafe delegate int RenderPlayers(int visitor);

	public Dictionary<uint, ClientData> ClientData = new Dictionary<uint, ClientData>();
	public Mutex ClientDataMutex = new Mutex();
	public unsafe PlayerSub* ClientVisitor = null;
	public unsafe PlayerSub* ClientNights = null;

	public TcpClient Server = new TcpClient();
	//public UdpClient UdpServer = new UdpClient();

	public uint ClientId = 0;

	public unsafe void ConnectToServer() {
		if (!ConnectToServerOnce) {
			ConnectToServerOnce = true;

			var playerSubCtor = _hooks.CreateWrapper<PlayerSubCtor>(0x56cff0, out _);

			ClientVisitor = (PlayerSub*)Memory.Instance.Allocate(0x1E8);
			playerSubCtor(2, (nuint)(*Globals.WorldManager)->Player, ClientVisitor);
			ClientVisitor->Animation->Motion->ThisNeedsToBe2OrAnimationsAreBrokenIDKWhy = 2;
			Console.WriteLine("new visitor: 0x" + ((int)ClientVisitor).ToString("X"));
			ClientNights = (PlayerSub*)Memory.Instance.Allocate(0x298);
			playerSubCtor(0, (nuint)(*Globals.WorldManager)->Player, ClientNights);
			ClientNights->Animation->Motion->ThisNeedsToBe2OrAnimationsAreBrokenIDKWhy = 2;
			Console.WriteLine("new nights: 0x" + ((int)ClientNights).ToString("X"));

			string[] asmCallRenderVisitor = {
				$"use32",
				$"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}",
				$"{_hooks.Utilities.GetAbsoluteCallMnemonics(RenderVisitor, out renderPlayersReverseWrapper)}",
				$"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}"
			};
			RenderPlayersAsmHook = _hooks.CreateAsmHook(
				asmCallRenderVisitor, 0x4AED24, AsmHookBehaviour.ExecuteFirst
			).Activate();
		}

		connectingToServer = true;
		new Thread(ConnectToServerRepeat).Start();
	}

	// signature: int __thiscall RenderRenderable3D(void *this)
	// location: 0x4AF610
	[Function(CallingConventions.MicrosoftThiscall)]
	public unsafe delegate int RenderRenderable3D(PlayerSub* renderable3d);

	public unsafe void RenderVisitor() {
		var renderable3d_render = _hooks.CreateWrapper<RenderRenderable3D>(0x4AF610, out _);
		ClientDataMutex.WaitOne();
		if (ClientVisitor != null && ClientNights != null) {
			foreach (var clientData in ClientData) {
				if (clientData.Key == ClientId) {
					continue;
				}
				var playerSub = clientData.Value.PlayerSubType == 0 ? ClientNights : ClientVisitor;
				playerSub->Animation->Pos = clientData.Value.Pos;
				playerSub->Animation->Rot = clientData.Value.Rot;
				playerSub->Animation->Motion->Animation = clientData.Value.Animation.Id;
				playerSub->Animation->Motion->Frame = clientData.Value.Animation.Frame;
				playerSub->Animation->Motion->FrameAlt = clientData.Value.Animation.FrameAlt;
				renderable3d_render(playerSub);
			}
		}
		ClientDataMutex.ReleaseMutex();
	}

	public unsafe void Send<T>(T data) {
		// serialize data
		var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
		var json_bytes = Encoding.UTF8.GetBytes(json);

		// send length as u16 in be
		var length = new byte[2];
		length[0] = (byte)(json_bytes.Length >> 8);
		length[1] = (byte)(json_bytes.Length & 0xFF);
		Server.GetStream().Write(length, 0, 2);

		// send serialized data
		Server.GetStream().Write(json_bytes, 0, json_bytes.Length);
	}

	public unsafe T Recv<T>() {
		// recv length as u16 in be
		var length = new byte[2];
		Server.GetStream().Read(length, 0, 2);
		var lengthInt = (length[0] << 8) | length[1];

		// recv serialized data
		var json_bytes = new byte[lengthInt];
		Server.GetStream().Read(json_bytes, 0, lengthInt);
		var json = Encoding.UTF8.GetString(json_bytes);

		//// Debug Output the received JSON
		//Console.WriteLine(json);

		// deserialize data
		return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
	}

	//// untested
	//public unsafe void SendUdp<T>(T data) {
	//	// serialize data
	//	var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
	//	var json_bytes = Encoding.UTF8.GetBytes(json);

	//	// send serialized data in one packet (turns out UDP doesn't need size?)
	//	UdpServer.Send(json_bytes, json_bytes.Length);
	//}

	//// untested
	//public unsafe T? RecvUdp<T>() where T : class {
	//	// recv serialized data
	//	var RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
	//	var json_bytes = UdpServer.Receive(ref RemoteIpEndPoint);
	//	var json = Encoding.UTF8.GetString(json_bytes);

	//	// deserialize data, return null if deserialization fails
	//	try {
	//		return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
	//	} catch (Exception) {
	//		return null;
	//	}
	//}

	public unsafe void ConnectToServerRepeat() {
		// convert _multiplayerHost to a string, excluding \0 characters, there is probably an better way to do this
		var ipString = "";
		for (int i = 0; i < _multiplayerHost.Length; ++i) {
			if (_multiplayerHost[i] == 0) {
				break;
			}
			ipString += (char)_multiplayerHost[i];
		}

		Server = new TcpClient();
		Server.Connect(ipString, _multiplayerPort);
		connectingToServer = false;
		if (!Server.Connected) {
			return;
		}

		//UdpServer = new UdpClient();
		//UdpServer.Connect(ipString, _multiplayerPort);
		//ushort udpPort = (ushort)((IPEndPoint)UdpServer.Client.LocalEndPoint).Port;

		var serverRequest = new ServerRequest {
			Version = new structs.Version { Major = 0, Minor = 0 },
			//UdpPort = udpPort
		};
		Send(serverRequest);

		var serverResponse = Recv<ServerResponse>();
		ClientId = serverResponse.Id;

		var world_manager = *(byte**)0x24C4EC4;

		while (true) {
			Thread.Sleep(1000 / 60);
			if (disconnectFromServer) {
				break;
			}
			var player = *(byte**)(world_manager + 0x50);
			if (player == null) {
				continue;
			}
			var playerSub = ((Player*)player)->PlayerSub;
			if (playerSub == null) {
				continue;
			}

			// todo: udp?
			Send(new ClientData {
				PlayerSubType = (uint)playerSub->Type,
				Pos = playerSub->Animation->Pos,
				Rot = playerSub->Animation->Rot,
				Animation = new ClientAnimationData {
					Id = playerSub->Animation->Motion->Animation,
					Frame = playerSub->Animation->Motion->Frame,
					FrameAlt = playerSub->Animation->Motion->FrameAlt
				}
			});
			if (disconnectFromServer || !Server.Connected) {
				break;
			}
			// receive other player positions from server
			var received = Recv<Dictionary<uint, ClientData>>();
			if (disconnectFromServer || !Server.Connected) {
				break;
			}
			if (received != null) {
				ClientDataMutex.WaitOne();
				ClientData = received;
				ClientDataMutex.ReleaseMutex();
			}
		}
		disconnectFromServer = false;
		Server.Close();
		ClientDataMutex.WaitOne();
		ClientData.Clear();
		// crashes? I guess we'll just leak memory!
		//ClientVisitor->Dtor();
		//Memory.Instance.Free((nuint)ClientVisitor);
		//ClientVisitor = null;
		//ClientNights->Dtor();
		//Memory.Instance.Free((nuint)ClientNights);
		//ClientNights = null;
		ClientDataMutex.ReleaseMutex();
	}

	#region Standard Overrides
	public override void ConfigurationUpdated(Config configuration)
	{
		// Apply settings from configuration.
		// ... your code here.
		_configuration = configuration;
		_logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
	}
	#endregion

	#region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	public Mod() { }
#pragma warning restore CS8618
	#endregion
}
