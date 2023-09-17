using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Sources;
using SharpDX;
using System.Runtime.InteropServices;

namespace nights.test.client.structs;

[StructLayout(LayoutKind.Sequential)]
public struct Vec3 {
	public float X { get; set; }
	public float Y { get; set; }
	public float Z { get; set; }
}

[StructLayout(LayoutKind.Sequential)]
public struct Rot3 {
	public short X { get; set; }
	public short Y { get; set; }
	public short Z { get; set; }
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct Character {
	[StructLayout(LayoutKind.Explicit)]
	private unsafe struct CharacterVFTable {
		[FieldOffset(0x0)]
		public IntPtr Dtor;
	}

	[FieldOffset(0x0)]
	private CharacterVFTable* _vftable;

	[Function(CallingConventions.MicrosoftThiscall)]
	private unsafe delegate Character* DtorT(Character* self, char a2);

	public Character* Dtor() {
		var fn = Globals.Hooks.CreateWrapper<DtorT>(_vftable->Dtor, out _);
		fixed (Character* self = &this) {
			var result = fn(self, (char)0);
			Memory.Instance.Free((nuint)self);
			return result;
		}
	}

	[FieldOffset(0x88)]
	public Animation* Animation;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct PlayerSub {
	[FieldOffset(0x88)]
	public Animation* Animation;

	[FieldOffset(0xEC)]
	public Player* Player;

	[FieldOffset(0xF0)]
	public PlayerSubType Type;
}


[StructLayout(LayoutKind.Explicit)]
public unsafe struct Player {
	[FieldOffset(0x60)]
	public PlayerSub* PlayerSub;

	[FieldOffset(0x64)]
	private PlayerSub* _playerSubsBegin;
	public PlayerSub* GetPlayerSub(PlayerSubType type) {
		fixed (PlayerSub** _playerSubs = &_playerSubsBegin) {
			return _playerSubs[(int)type];
		}
	}
}

public enum PlayerSubType {
	Nights,
	Elliot,
	Claris,
	ElliotTwinSeeds,
	ClarisTwinSeeds,
	OtherNightsWizemanFight
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct WorldManager {
	[FieldOffset(0x50)]
	public Player* Player;
}

public unsafe struct Globals {
	public static unsafe WorldManager** WorldManager = (WorldManager**)0x24C4EC4;

	public static IReloadedHooks Hooks;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct Animation {
	[FieldOffset(0x20)]
	public Motion* Motion;
	[FieldOffset(0x30)]
	public Vec3 Pos;
	[FieldOffset(0x3C)]
	public Rot3 Rot;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct Motion {
	[FieldOffset(0x14)]
	public int Animation;
	[FieldOffset(0x18)]
	public int Frame;
	[FieldOffset(0x1290)]
	public int FrameAlt;
	[FieldOffset(0x12A8)]
	public int ThisNeedsToBe2OrAnimationsAreBrokenIDKWhy;
}

public struct ServerRequest {
	public Version Version { get; set; }
	public ushort UdpPort { get; set; }
}

public struct Version {
	public byte Major { get; set; }
	public byte Minor { get; set; }
}

public struct ServerResponse {
	public uint Id { get; set; }
}

public struct ClientData {
	public uint PlayerSubType { get; set; }
	public Vec3 Pos { get; set; }
	public Rot3 Rot { get; set; }
	public ClientAnimationData Animation { get; set; }
}

public struct ClientAnimationData {
	public int Id { get; set; }
	public int Frame { get; set; }
	public int FrameAlt { get; set; }
}
