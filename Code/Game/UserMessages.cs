using GoldSrc;

namespace HalfLife;

public static class UserMessages
{
	public const int HudPrintNotify = 1;
	public const int HudPrintConsole = 2;
	public const int HudPrintTalk = 3;
	public const int HudPrintCenter = 4;

	public static int Shake { get; private set; }
	public static int Fade { get; private set; }
	public static int SelAmmo { get; private set; }
	public static int Flashlight { get; private set; }
	public static int FlashBattery { get; private set; }
	public static int ResetHud { get; private set; }
	public static int InitHud { get; private set; }
	public static int ShowGameTitle { get; private set; }
	public static int CurWeapon { get; private set; }
	public static int Health { get; private set; }
	public static int Damage { get; private set; }
	public static int Battery { get; private set; }
	public static int Train { get; private set; }
	public static int Logo { get; set; }
	public static int WeaponList { get; private set; }
	public static int AmmoX { get; private set; }
	public static int HudText { get; private set; }
	public static int DeathMsg { get; private set; }
	public static int ScoreInfo { get; private set; }
	public static int TeamInfo { get; private set; }
	public static int TeamScore { get; private set; }
	public static int GameMode { get; private set; }
	public static int Motd { get; private set; }
	public static int ServerName { get; private set; }
	public static int AmmoPickup { get; private set; }
	public static int WeapPickup { get; private set; }
	public static int ItemPickup { get; private set; }
	public static int HideWeapon { get; private set; }
	public static int SayText { get; private set; }
	public static int TextMsg { get; private set; }
	public static int SetFov { get; private set; }
	public static int ShowMenu { get; private set; }
	public static int GeigerRange { get; private set; }
	public static int TeamNames { get; private set; }
	public static int StatusText { get; private set; }
	public static int StatusValue { get; private set; }

	public static void Link()
	{
		if ( SelAmmo != 0 )
			return;

		SelAmmo = Server.RegUserMsg( "SelAmmo", 4 );
		CurWeapon = Server.RegUserMsg( "CurWeapon", 3 );
		GeigerRange = Server.RegUserMsg( "Geiger", 1 );
		Flashlight = Server.RegUserMsg( "Flashlight", 2 );
		FlashBattery = Server.RegUserMsg( "FlashBat", 1 );
		Health = Server.RegUserMsg( "Health", 1 );
		Damage = Server.RegUserMsg( "Damage", 12 );
		Battery = Server.RegUserMsg( "Battery", 2 );
		Train = Server.RegUserMsg( "Train", 1 );
		HudText = Server.RegUserMsg( "HudText", -1 );
		SayText = Server.RegUserMsg( "SayText", -1 );
		TextMsg = Server.RegUserMsg( "TextMsg", -1 );
		WeaponList = Server.RegUserMsg( "WeaponList", -1 );
		ResetHud = Server.RegUserMsg( "ResetHUD", 1 );
		InitHud = Server.RegUserMsg( "InitHUD", 0 );
		ShowGameTitle = Server.RegUserMsg( "GameTitle", 1 );
		DeathMsg = Server.RegUserMsg( "DeathMsg", -1 );
		ScoreInfo = Server.RegUserMsg( "ScoreInfo", 9 );
		TeamInfo = Server.RegUserMsg( "TeamInfo", -1 );
		TeamScore = Server.RegUserMsg( "TeamScore", -1 );
		GameMode = Server.RegUserMsg( "GameMode", 1 );
		Motd = Server.RegUserMsg( "MOTD", -1 );
		ServerName = Server.RegUserMsg( "ServerName", -1 );
		AmmoPickup = Server.RegUserMsg( "AmmoPickup", 2 );
		WeapPickup = Server.RegUserMsg( "WeapPickup", 1 );
		ItemPickup = Server.RegUserMsg( "ItemPickup", -1 );
		HideWeapon = Server.RegUserMsg( "HideWeapon", 1 );
		SetFov = Server.RegUserMsg( "SetFOV", 1 );
		ShowMenu = Server.RegUserMsg( "ShowMenu", -1 );
		Shake = Server.RegUserMsg( "ScreenShake", 6 );
		Fade = Server.RegUserMsg( "ScreenFade", 10 );
		AmmoX = Server.RegUserMsg( "AmmoX", 2 );
		TeamNames = Server.RegUserMsg( "TeamNames", -1 );
		StatusText = Server.RegUserMsg( "StatusText", -1 );
		StatusValue = Server.RegUserMsg( "StatusValue", 3 );
	}
}
