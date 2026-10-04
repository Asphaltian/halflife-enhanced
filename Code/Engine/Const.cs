using System;

namespace GoldSrc;

[Flags]
public enum EntityFlags
{
	Fly = 1 << 0,
	Swim = 1 << 1,
	Conveyor = 1 << 2,
	Client = 1 << 3,
	InWater = 1 << 4,
	Monster = 1 << 5,
	GodMode = 1 << 6,
	NoTarget = 1 << 7,
	SkipLocalHost = 1 << 8,
	OnGround = 1 << 9,
	PartialGround = 1 << 10,
	WaterJump = 1 << 11,
	Frozen = 1 << 12,
	FakeClient = 1 << 13,
	Ducking = 1 << 14,
	Float = 1 << 15,
	Graphed = 1 << 16,
	ImmuneWater = 1 << 17,
	ImmuneSlime = 1 << 18,
	ImmuneLava = 1 << 19,
	Proxy = 1 << 20,
	AlwaysThink = 1 << 21,
	BaseVelocity = 1 << 22,
	MonsterClip = 1 << 23,
	OnTrain = 1 << 24,
	WorldBrush = 1 << 25,
	Spectator = 1 << 26,
	CustomEntity = 1 << 29,
	KillMe = 1 << 30,
	Dormant = 1 << 31
}

[Flags]
public enum InButtons : ushort
{
	Attack = 1 << 0,
	Jump = 1 << 1,
	Duck = 1 << 2,
	Forward = 1 << 3,
	Back = 1 << 4,
	Use = 1 << 5,
	Cancel = 1 << 6,
	Left = 1 << 7,
	Right = 1 << 8,
	MoveLeft = 1 << 9,
	MoveRight = 1 << 10,
	Attack2 = 1 << 11,
	Run = 1 << 12,
	Reload = 1 << 13,
	Alt1 = 1 << 14,
	Score = 1 << 15
}

public enum MoveType
{
	None = 0,
	Walk = 3,
	Step = 4,
	Fly = 5,
	Toss = 6,
	Push = 7,
	NoClip = 8,
	FlyMissile = 9,
	Bounce = 10,
	BounceMissile = 11,
	Follow = 12,
	PushStep = 13
}

public enum Solid
{
	Not = 0,
	Trigger = 1,
	BBox = 2,
	SlideBox = 3,
	Bsp = 4
}

[Flags]
public enum Effects
{
	BrightField = 1,
	MuzzleFlash = 2,
	BrightLight = 4,
	DimLight = 8,
	InvLight = 16,
	NoInterp = 32,
	Light = 64,
	NoDraw = 128,
	NightVision = 256,
	SniperLaser = 512,
	FiberCamera = 1024
}

public enum RenderMode
{
	Normal,
	TransColor,
	TransTexture,
	Glow,
	TransAlpha,
	TransAdd
}

public enum RenderFx
{
	None,
	PulseSlow,
	PulseFast,
	PulseSlowWide,
	PulseFastWide,
	FadeSlow,
	FadeFast,
	SolidSlow,
	SolidFast,
	StrobeSlow,
	StrobeFast,
	StrobeFaster,
	FlickerSlow,
	FlickerFast,
	NoDissipation,
	Distort,
	Hologram,
	DeadPlayer,
	Explode,
	GlowShell,
	ClampMinScale,
	LightMultiplier
}

public static class TempEntity
{
	public const int BeamPoints = 0;
	public const int BeamEntPoint = 1;
	public const int Gunshot = 2;
	public const int Explosion = 3;
	public const int TarExplosion = 4;
	public const int Smoke = 5;
	public const int Tracer = 6;
	public const int Lightning = 7;
	public const int BeamEnts = 8;
	public const int Sparks = 9;
	public const int LavaSplash = 10;
	public const int Teleport = 11;
	public const int Explosion2 = 12;
	public const int BspDecal = 13;
	public const int Implosion = 14;
	public const int SpriteTrail = 15;
	public const int Beam = 16;
	public const int Sprite = 17;
	public const int BeamSprite = 18;
	public const int BeamTorus = 19;
	public const int BeamDisk = 20;
	public const int BeamCylinder = 21;
	public const int BeamFollow = 22;
	public const int GlowSprite = 23;
	public const int BeamRing = 24;
	public const int StreakSplash = 25;
	public const int BeamHose = 26;
	public const int DLight = 27;
	public const int ELight = 28;
	public const int TextMessage = 29;
	public const int Line = 30;
	public const int Box = 31;
	public const int KillBeam = 99;
	public const int LargeFunnel = 100;
	public const int BloodStream = 101;
	public const int ShowLine = 102;
	public const int Blood = 103;
	public const int Decal = 104;
	public const int Fizz = 105;
	public const int Model = 106;
	public const int ExplodeModel = 107;
	public const int BreakModel = 108;
	public const int GunshotDecal = 109;
	public const int SpriteSpray = 110;
	public const int ArmorRicochet = 111;
	public const int PlayerDecal = 112;
	public const int Bubbles = 113;
	public const int BubbleTrail = 114;
	public const int BloodSprite = 115;
	public const int WorldDecal = 116;
	public const int WorldDecalHigh = 117;
	public const int DecalHigh = 118;
	public const int Projectile = 119;
	public const int Spray = 120;
	public const int PlayerSprites = 121;
	public const int ParticleBurst = 122;
	public const int FireField = 123;
	public const int PlayerAttachment = 124;
	public const int KillPlayerAttachments = 125;
	public const int MultiGunshot = 126;
	public const int UserTracer = 127;

	public const int ExplosionNone = 0;
	public const int ExplosionNoAdditive = 1;
	public const int ExplosionNoDLights = 2;
	public const int ExplosionNoSound = 4;
	public const int ExplosionNoParticles = 8;

	public const int BounceNull = 0;
	public const int BounceShell = 1;
	public const int BounceShotShell = 2;
}
