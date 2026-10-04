using GoldSrc;

namespace HalfLife;

public static class Decals
{
	public const int Gunshot1 = 0;
	public const int Gunshot2 = 1;
	public const int Gunshot3 = 2;
	public const int Gunshot4 = 3;
	public const int Gunshot5 = 4;
	public const int Lambda1 = 5;
	public const int Lambda2 = 6;
	public const int Lambda3 = 7;
	public const int Lambda4 = 8;
	public const int Lambda5 = 9;
	public const int Lambda6 = 10;
	public const int Scorch1 = 11;
	public const int Scorch2 = 12;
	public const int Blood1 = 13;
	public const int Blood2 = 14;
	public const int Blood3 = 15;
	public const int Blood4 = 16;
	public const int Blood5 = 17;
	public const int Blood6 = 18;
	public const int YellowBlood1 = 19;
	public const int YellowBlood2 = 20;
	public const int YellowBlood3 = 21;
	public const int YellowBlood4 = 22;
	public const int YellowBlood5 = 23;
	public const int YellowBlood6 = 24;
	public const int GlassBreak1 = 25;
	public const int GlassBreak2 = 26;
	public const int GlassBreak3 = 27;
	public const int BigShot1 = 28;
	public const int BigShot2 = 29;
	public const int BigShot3 = 30;
	public const int BigShot4 = 31;
	public const int BigShot5 = 32;
	public const int Spit1 = 33;
	public const int Spit2 = 34;
	public const int BulletProof1 = 35;
	public const int GargStomp1 = 36;
	public const int SmallScorch1 = 37;
	public const int SmallScorch2 = 38;
	public const int SmallScorch3 = 39;
	public const int MommaBirth = 40;
	public const int MommaSplat = 41;

	private static readonly string[] Names =
	[
		"{shot1", "{shot2", "{shot3", "{shot4", "{shot5",
		"{lambda01", "{lambda02", "{lambda03", "{lambda04", "{lambda05", "{lambda06",
		"{scorch1", "{scorch2",
		"{blood1", "{blood2", "{blood3", "{blood4", "{blood5", "{blood6",
		"{yblood1", "{yblood2", "{yblood3", "{yblood4", "{yblood5", "{yblood6",
		"{break1", "{break2", "{break3",
		"{bigshot1", "{bigshot2", "{bigshot3", "{bigshot4", "{bigshot5",
		"{spit1", "{spit2",
		"{bproof1",
		"{gargstomp",
		"{smscorch1", "{smscorch2", "{smscorch3",
		"{mommablob", "{mommablob"
	];

	public static int[] Index { get; private set; } = new int[Names.Length];

	public static void Init( Server server )
	{
		for ( var i = 0; i < Names.Length; i++ )
			Index[i] = server.DecalIndex( Names[i] );
	}
}

public static class GameModels
{
	public const string LaserName = "sprites/laserbeam.spr";

	public static int Laser { get; set; }
	public static int LaserDot { get; set; }
	public static int Fireball { get; set; }
	public static int Smoke { get; set; }
	public static int WaterExplosion { get; set; }
	public static int Bubbles { get; set; }
	public static int BloodDrop { get; set; }
	public static int BloodSpray { get; set; }
}

public struct HudTextParms
{
	public float X;
	public float Y;
	public int Effect;
	public byte R1;
	public byte G1;
	public byte B1;
	public byte A1;
	public byte R2;
	public byte G2;
	public byte B2;
	public byte A2;
	public float FadeInTime;
	public float FadeOutTime;
	public float HoldTime;
	public float FxTime;
	public int Channel;
}
