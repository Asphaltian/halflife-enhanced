namespace HalfLife;

public static class Hltv
{
	public const int TypeClient = 0;
	public const int TypeProxy = 1;
	public const int TypeCommentator = 3;
	public const int TypeDemo = 4;

	public const int Active = 0;
	public const int Status = 1;
	public const int Listen = 2;

	public const int CmdNone = 0;
	public const int CmdStart = 1;
	public const int CmdEvent = 2;
	public const int CmdMode = 3;
	public const int CmdCamera = 4;
	public const int CmdTimeScale = 5;
	public const int CmdMessage = 6;
	public const int CmdSound = 7;
	public const int CmdStatus = 8;
	public const int CmdBanner = 9;
	public const int CmdStuffText = 10;
	public const int CmdChase = 11;
	public const int CmdInEye = 12;
	public const int CmdMap = 13;
	public const int CmdCamPath = 14;
	public const int CmdWaypoints = 15;
	public const int CmdLast = 15;

	public const int FlagPrioMask = 0x0F;
	public const int FlagSide = 1 << 4;
	public const int FlagDramatic = 1 << 5;
	public const int FlagSlowMotion = 1 << 6;
	public const int FlagFacePlayer = 1 << 7;
	public const int FlagIntro = 1 << 8;
	public const int FlagFinal = 1 << 9;
	public const int FlagNoRandom = 1 << 10;

	public const int FlagStartPath = 1;
	public const int FlagSlowStart = 2;
	public const int FlagSlowEnd = 4;
}
