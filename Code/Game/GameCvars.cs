namespace HalfLife;

public static class GameCvars
{
	[ConVar( "displaysoundlist" )]
	private static float Displaysoundlist { get; set; } = 0f;

	[ConVar( "allow_spectators", ConVarFlags.Replicated )]
	private static float AllowSpectators { get; set => GoldSrc.Server.SetServerCvar( "allow_spectators", ref field, value ); } = 0f;

	[ConVar( "mp_teamplay", ConVarFlags.Replicated )]
	private static float MpTeamplay { get; set => GoldSrc.Server.SetServerCvar( "mp_teamplay", ref field, value ); } = 0f;

	[ConVar( "mp_fraglimit", ConVarFlags.Replicated )]
	private static float MpFraglimit { get; set => GoldSrc.Server.SetServerCvar( "mp_fraglimit", ref field, value ); } = 0f;

	[ConVar( "mp_timelimit", ConVarFlags.Replicated )]
	private static float MpTimelimit { get; set => GoldSrc.Server.SetServerCvar( "mp_timelimit", ref field, value ); } = 0f;

	[ConVar( "mp_fragsleft", ConVarFlags.Replicated )]
	private static float MpFragsleft { get; set => GoldSrc.Server.SetServerCvar( "mp_fragsleft", ref field, value ); } = 0f;

	[ConVar( "mp_timeleft", ConVarFlags.Replicated )]
	private static float MpTimeleft { get; set => GoldSrc.Server.SetServerCvar( "mp_timeleft", ref field, value ); } = 0f;

	[ConVar( "mp_friendlyfire", ConVarFlags.Replicated )]
	private static float MpFriendlyfire { get; set => GoldSrc.Server.SetServerCvar( "mp_friendlyfire", ref field, value ); } = 0f;

	[ConVar( "mp_falldamage", ConVarFlags.Replicated )]
	private static float MpFalldamage { get; set => GoldSrc.Server.SetServerCvar( "mp_falldamage", ref field, value ); } = 0f;

	[ConVar( "mp_weaponstay", ConVarFlags.Replicated )]
	private static float MpWeaponstay { get; set => GoldSrc.Server.SetServerCvar( "mp_weaponstay", ref field, value ); } = 0f;

	[ConVar( "mp_forcerespawn", ConVarFlags.Replicated )]
	private static float MpForcerespawn { get; set => GoldSrc.Server.SetServerCvar( "mp_forcerespawn", ref field, value ); } = 1f;

	[ConVar( "mp_flashlight", ConVarFlags.Replicated )]
	private static float MpFlashlight { get; set => GoldSrc.Server.SetServerCvar( "mp_flashlight", ref field, value ); } = 0f;

	[ConVar( "mp_autocrosshair", ConVarFlags.Replicated )]
	private static float MpAutocrosshair { get; set => GoldSrc.Server.SetServerCvar( "mp_autocrosshair", ref field, value ); } = 1f;

	[ConVar( "decalfrequency", ConVarFlags.Replicated )]
	private static float Decalfrequency { get; set => GoldSrc.Server.SetServerCvar( "decalfrequency", ref field, value ); } = 30f;

	[ConVar( "mp_teamlist", ConVarFlags.Replicated )]
	private static string MpTeamlist { get; set => GoldSrc.Server.SetServerCvar( "mp_teamlist", ref field, value ); } = "hgrunt;scientist";

	[ConVar( "mp_teamoverride" )]
	private static float MpTeamoverride { get; set; } = 1f;

	[ConVar( "mp_defaultteam" )]
	private static float MpDefaultteam { get; set; } = 0f;

	[ConVar( "mp_allowmonsters", ConVarFlags.Replicated )]
	private static float MpAllowmonsters { get; set => GoldSrc.Server.SetServerCvar( "mp_allowmonsters", ref field, value ); } = 0f;

	[ConVar( "mp_chattime", ConVarFlags.Replicated )]
	private static float MpChattime { get; set => GoldSrc.Server.SetServerCvar( "mp_chattime", ref field, value ); } = 10f;

	[ConVar( "sv_busters" )]
	private static float SvBusters { get; set; } = 0f;

	[ConVar( "sk_agrunt_health1" )]
	private static float SkAgruntHealth1 { get; set; } = 0f;

	[ConVar( "sk_agrunt_health2" )]
	private static float SkAgruntHealth2 { get; set; } = 0f;

	[ConVar( "sk_agrunt_health3" )]
	private static float SkAgruntHealth3 { get; set; } = 0f;

	[ConVar( "sk_agrunt_dmg_punch1" )]
	private static float SkAgruntDmgPunch1 { get; set; } = 0f;

	[ConVar( "sk_agrunt_dmg_punch2" )]
	private static float SkAgruntDmgPunch2 { get; set; } = 0f;

	[ConVar( "sk_agrunt_dmg_punch3" )]
	private static float SkAgruntDmgPunch3 { get; set; } = 0f;

	[ConVar( "sk_apache_health1" )]
	private static float SkApacheHealth1 { get; set; } = 0f;

	[ConVar( "sk_apache_health2" )]
	private static float SkApacheHealth2 { get; set; } = 0f;

	[ConVar( "sk_apache_health3" )]
	private static float SkApacheHealth3 { get; set; } = 0f;

	[ConVar( "sk_barney_health1" )]
	private static float SkBarneyHealth1 { get; set; } = 0f;

	[ConVar( "sk_barney_health2" )]
	private static float SkBarneyHealth2 { get; set; } = 0f;

	[ConVar( "sk_barney_health3" )]
	private static float SkBarneyHealth3 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_health1" )]
	private static float SkBullsquidHealth1 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_health2" )]
	private static float SkBullsquidHealth2 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_health3" )]
	private static float SkBullsquidHealth3 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_dmg_bite1" )]
	private static float SkBullsquidDmgBite1 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_dmg_bite2" )]
	private static float SkBullsquidDmgBite2 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_dmg_bite3" )]
	private static float SkBullsquidDmgBite3 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_dmg_whip1" )]
	private static float SkBullsquidDmgWhip1 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_dmg_whip2" )]
	private static float SkBullsquidDmgWhip2 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_dmg_whip3" )]
	private static float SkBullsquidDmgWhip3 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_dmg_spit1" )]
	private static float SkBullsquidDmgSpit1 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_dmg_spit2" )]
	private static float SkBullsquidDmgSpit2 { get; set; } = 0f;

	[ConVar( "sk_bullsquid_dmg_spit3" )]
	private static float SkBullsquidDmgSpit3 { get; set; } = 0f;

	[ConVar( "sk_bigmomma_health_factor1" )]
	private static float SkBigmommaHealthFactor1 { get; set; } = 1f;

	[ConVar( "sk_bigmomma_health_factor2" )]
	private static float SkBigmommaHealthFactor2 { get; set; } = 1f;

	[ConVar( "sk_bigmomma_health_factor3" )]
	private static float SkBigmommaHealthFactor3 { get; set; } = 1f;

	[ConVar( "sk_bigmomma_dmg_slash1" )]
	private static float SkBigmommaDmgSlash1 { get; set; } = 50f;

	[ConVar( "sk_bigmomma_dmg_slash2" )]
	private static float SkBigmommaDmgSlash2 { get; set; } = 50f;

	[ConVar( "sk_bigmomma_dmg_slash3" )]
	private static float SkBigmommaDmgSlash3 { get; set; } = 50f;

	[ConVar( "sk_bigmomma_dmg_blast1" )]
	private static float SkBigmommaDmgBlast1 { get; set; } = 100f;

	[ConVar( "sk_bigmomma_dmg_blast2" )]
	private static float SkBigmommaDmgBlast2 { get; set; } = 100f;

	[ConVar( "sk_bigmomma_dmg_blast3" )]
	private static float SkBigmommaDmgBlast3 { get; set; } = 100f;

	[ConVar( "sk_bigmomma_radius_blast1" )]
	private static float SkBigmommaRadiusBlast1 { get; set; } = 250f;

	[ConVar( "sk_bigmomma_radius_blast2" )]
	private static float SkBigmommaRadiusBlast2 { get; set; } = 250f;

	[ConVar( "sk_bigmomma_radius_blast3" )]
	private static float SkBigmommaRadiusBlast3 { get; set; } = 250f;

	[ConVar( "sk_gargantua_health1" )]
	private static float SkGargantuaHealth1 { get; set; } = 0f;

	[ConVar( "sk_gargantua_health2" )]
	private static float SkGargantuaHealth2 { get; set; } = 0f;

	[ConVar( "sk_gargantua_health3" )]
	private static float SkGargantuaHealth3 { get; set; } = 0f;

	[ConVar( "sk_gargantua_dmg_slash1" )]
	private static float SkGargantuaDmgSlash1 { get; set; } = 0f;

	[ConVar( "sk_gargantua_dmg_slash2" )]
	private static float SkGargantuaDmgSlash2 { get; set; } = 0f;

	[ConVar( "sk_gargantua_dmg_slash3" )]
	private static float SkGargantuaDmgSlash3 { get; set; } = 0f;

	[ConVar( "sk_gargantua_dmg_fire1" )]
	private static float SkGargantuaDmgFire1 { get; set; } = 0f;

	[ConVar( "sk_gargantua_dmg_fire2" )]
	private static float SkGargantuaDmgFire2 { get; set; } = 0f;

	[ConVar( "sk_gargantua_dmg_fire3" )]
	private static float SkGargantuaDmgFire3 { get; set; } = 0f;

	[ConVar( "sk_gargantua_dmg_stomp1" )]
	private static float SkGargantuaDmgStomp1 { get; set; } = 0f;

	[ConVar( "sk_gargantua_dmg_stomp2" )]
	private static float SkGargantuaDmgStomp2 { get; set; } = 0f;

	[ConVar( "sk_gargantua_dmg_stomp3" )]
	private static float SkGargantuaDmgStomp3 { get; set; } = 0f;

	[ConVar( "sk_hassassin_health1" )]
	private static float SkHassassinHealth1 { get; set; } = 0f;

	[ConVar( "sk_hassassin_health2" )]
	private static float SkHassassinHealth2 { get; set; } = 0f;

	[ConVar( "sk_hassassin_health3" )]
	private static float SkHassassinHealth3 { get; set; } = 0f;

	[ConVar( "sk_headcrab_health1" )]
	private static float SkHeadcrabHealth1 { get; set; } = 0f;

	[ConVar( "sk_headcrab_health2" )]
	private static float SkHeadcrabHealth2 { get; set; } = 0f;

	[ConVar( "sk_headcrab_health3" )]
	private static float SkHeadcrabHealth3 { get; set; } = 0f;

	[ConVar( "sk_headcrab_dmg_bite1" )]
	private static float SkHeadcrabDmgBite1 { get; set; } = 0f;

	[ConVar( "sk_headcrab_dmg_bite2" )]
	private static float SkHeadcrabDmgBite2 { get; set; } = 0f;

	[ConVar( "sk_headcrab_dmg_bite3" )]
	private static float SkHeadcrabDmgBite3 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_health1" )]
	private static float SkHgruntHealth1 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_health2" )]
	private static float SkHgruntHealth2 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_health3" )]
	private static float SkHgruntHealth3 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_kick1" )]
	private static float SkHgruntKick1 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_kick2" )]
	private static float SkHgruntKick2 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_kick3" )]
	private static float SkHgruntKick3 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_pellets1" )]
	private static float SkHgruntPellets1 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_pellets2" )]
	private static float SkHgruntPellets2 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_pellets3" )]
	private static float SkHgruntPellets3 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_gspeed1" )]
	private static float SkHgruntGspeed1 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_gspeed2" )]
	private static float SkHgruntGspeed2 { get; set; } = 0f;

	[ConVar( "sk_hgrunt_gspeed3" )]
	private static float SkHgruntGspeed3 { get; set; } = 0f;

	[ConVar( "sk_houndeye_health1" )]
	private static float SkHoundeyeHealth1 { get; set; } = 0f;

	[ConVar( "sk_houndeye_health2" )]
	private static float SkHoundeyeHealth2 { get; set; } = 0f;

	[ConVar( "sk_houndeye_health3" )]
	private static float SkHoundeyeHealth3 { get; set; } = 0f;

	[ConVar( "sk_houndeye_dmg_blast1" )]
	private static float SkHoundeyeDmgBlast1 { get; set; } = 0f;

	[ConVar( "sk_houndeye_dmg_blast2" )]
	private static float SkHoundeyeDmgBlast2 { get; set; } = 0f;

	[ConVar( "sk_houndeye_dmg_blast3" )]
	private static float SkHoundeyeDmgBlast3 { get; set; } = 0f;

	[ConVar( "sk_islave_health1" )]
	private static float SkIslaveHealth1 { get; set; } = 0f;

	[ConVar( "sk_islave_health2" )]
	private static float SkIslaveHealth2 { get; set; } = 0f;

	[ConVar( "sk_islave_health3" )]
	private static float SkIslaveHealth3 { get; set; } = 0f;

	[ConVar( "sk_islave_dmg_claw1" )]
	private static float SkIslaveDmgClaw1 { get; set; } = 0f;

	[ConVar( "sk_islave_dmg_claw2" )]
	private static float SkIslaveDmgClaw2 { get; set; } = 0f;

	[ConVar( "sk_islave_dmg_claw3" )]
	private static float SkIslaveDmgClaw3 { get; set; } = 0f;

	[ConVar( "sk_islave_dmg_clawrake1" )]
	private static float SkIslaveDmgClawrake1 { get; set; } = 0f;

	[ConVar( "sk_islave_dmg_clawrake2" )]
	private static float SkIslaveDmgClawrake2 { get; set; } = 0f;

	[ConVar( "sk_islave_dmg_clawrake3" )]
	private static float SkIslaveDmgClawrake3 { get; set; } = 0f;

	[ConVar( "sk_islave_dmg_zap1" )]
	private static float SkIslaveDmgZap1 { get; set; } = 0f;

	[ConVar( "sk_islave_dmg_zap2" )]
	private static float SkIslaveDmgZap2 { get; set; } = 0f;

	[ConVar( "sk_islave_dmg_zap3" )]
	private static float SkIslaveDmgZap3 { get; set; } = 0f;

	[ConVar( "sk_ichthyosaur_health1" )]
	private static float SkIchthyosaurHealth1 { get; set; } = 0f;

	[ConVar( "sk_ichthyosaur_health2" )]
	private static float SkIchthyosaurHealth2 { get; set; } = 0f;

	[ConVar( "sk_ichthyosaur_health3" )]
	private static float SkIchthyosaurHealth3 { get; set; } = 0f;

	[ConVar( "sk_ichthyosaur_shake1" )]
	private static float SkIchthyosaurShake1 { get; set; } = 0f;

	[ConVar( "sk_ichthyosaur_shake2" )]
	private static float SkIchthyosaurShake2 { get; set; } = 0f;

	[ConVar( "sk_ichthyosaur_shake3" )]
	private static float SkIchthyosaurShake3 { get; set; } = 0f;

	[ConVar( "sk_leech_health1" )]
	private static float SkLeechHealth1 { get; set; } = 0f;

	[ConVar( "sk_leech_health2" )]
	private static float SkLeechHealth2 { get; set; } = 0f;

	[ConVar( "sk_leech_health3" )]
	private static float SkLeechHealth3 { get; set; } = 0f;

	[ConVar( "sk_leech_dmg_bite1" )]
	private static float SkLeechDmgBite1 { get; set; } = 0f;

	[ConVar( "sk_leech_dmg_bite2" )]
	private static float SkLeechDmgBite2 { get; set; } = 0f;

	[ConVar( "sk_leech_dmg_bite3" )]
	private static float SkLeechDmgBite3 { get; set; } = 0f;

	[ConVar( "sk_controller_health1" )]
	private static float SkControllerHealth1 { get; set; } = 0f;

	[ConVar( "sk_controller_health2" )]
	private static float SkControllerHealth2 { get; set; } = 0f;

	[ConVar( "sk_controller_health3" )]
	private static float SkControllerHealth3 { get; set; } = 0f;

	[ConVar( "sk_controller_dmgzap1" )]
	private static float SkControllerDmgzap1 { get; set; } = 0f;

	[ConVar( "sk_controller_dmgzap2" )]
	private static float SkControllerDmgzap2 { get; set; } = 0f;

	[ConVar( "sk_controller_dmgzap3" )]
	private static float SkControllerDmgzap3 { get; set; } = 0f;

	[ConVar( "sk_controller_speedball1" )]
	private static float SkControllerSpeedball1 { get; set; } = 0f;

	[ConVar( "sk_controller_speedball2" )]
	private static float SkControllerSpeedball2 { get; set; } = 0f;

	[ConVar( "sk_controller_speedball3" )]
	private static float SkControllerSpeedball3 { get; set; } = 0f;

	[ConVar( "sk_controller_dmgball1" )]
	private static float SkControllerDmgball1 { get; set; } = 0f;

	[ConVar( "sk_controller_dmgball2" )]
	private static float SkControllerDmgball2 { get; set; } = 0f;

	[ConVar( "sk_controller_dmgball3" )]
	private static float SkControllerDmgball3 { get; set; } = 0f;

	[ConVar( "sk_nihilanth_health1" )]
	private static float SkNihilanthHealth1 { get; set; } = 0f;

	[ConVar( "sk_nihilanth_health2" )]
	private static float SkNihilanthHealth2 { get; set; } = 0f;

	[ConVar( "sk_nihilanth_health3" )]
	private static float SkNihilanthHealth3 { get; set; } = 0f;

	[ConVar( "sk_nihilanth_zap1" )]
	private static float SkNihilanthZap1 { get; set; } = 0f;

	[ConVar( "sk_nihilanth_zap2" )]
	private static float SkNihilanthZap2 { get; set; } = 0f;

	[ConVar( "sk_nihilanth_zap3" )]
	private static float SkNihilanthZap3 { get; set; } = 0f;

	[ConVar( "sk_scientist_health1" )]
	private static float SkScientistHealth1 { get; set; } = 0f;

	[ConVar( "sk_scientist_health2" )]
	private static float SkScientistHealth2 { get; set; } = 0f;

	[ConVar( "sk_scientist_health3" )]
	private static float SkScientistHealth3 { get; set; } = 0f;

	[ConVar( "sk_snark_health1" )]
	private static float SkSnarkHealth1 { get; set; } = 0f;

	[ConVar( "sk_snark_health2" )]
	private static float SkSnarkHealth2 { get; set; } = 0f;

	[ConVar( "sk_snark_health3" )]
	private static float SkSnarkHealth3 { get; set; } = 0f;

	[ConVar( "sk_snark_dmg_bite1" )]
	private static float SkSnarkDmgBite1 { get; set; } = 0f;

	[ConVar( "sk_snark_dmg_bite2" )]
	private static float SkSnarkDmgBite2 { get; set; } = 0f;

	[ConVar( "sk_snark_dmg_bite3" )]
	private static float SkSnarkDmgBite3 { get; set; } = 0f;

	[ConVar( "sk_snark_dmg_pop1" )]
	private static float SkSnarkDmgPop1 { get; set; } = 0f;

	[ConVar( "sk_snark_dmg_pop2" )]
	private static float SkSnarkDmgPop2 { get; set; } = 0f;

	[ConVar( "sk_snark_dmg_pop3" )]
	private static float SkSnarkDmgPop3 { get; set; } = 0f;

	[ConVar( "sk_zombie_health1" )]
	private static float SkZombieHealth1 { get; set; } = 0f;

	[ConVar( "sk_zombie_health2" )]
	private static float SkZombieHealth2 { get; set; } = 0f;

	[ConVar( "sk_zombie_health3" )]
	private static float SkZombieHealth3 { get; set; } = 0f;

	[ConVar( "sk_zombie_dmg_one_slash1" )]
	private static float SkZombieDmgOneSlash1 { get; set; } = 0f;

	[ConVar( "sk_zombie_dmg_one_slash2" )]
	private static float SkZombieDmgOneSlash2 { get; set; } = 0f;

	[ConVar( "sk_zombie_dmg_one_slash3" )]
	private static float SkZombieDmgOneSlash3 { get; set; } = 0f;

	[ConVar( "sk_zombie_dmg_both_slash1" )]
	private static float SkZombieDmgBothSlash1 { get; set; } = 0f;

	[ConVar( "sk_zombie_dmg_both_slash2" )]
	private static float SkZombieDmgBothSlash2 { get; set; } = 0f;

	[ConVar( "sk_zombie_dmg_both_slash3" )]
	private static float SkZombieDmgBothSlash3 { get; set; } = 0f;

	[ConVar( "sk_turret_health1" )]
	private static float SkTurretHealth1 { get; set; } = 0f;

	[ConVar( "sk_turret_health2" )]
	private static float SkTurretHealth2 { get; set; } = 0f;

	[ConVar( "sk_turret_health3" )]
	private static float SkTurretHealth3 { get; set; } = 0f;

	[ConVar( "sk_miniturret_health1" )]
	private static float SkMiniturretHealth1 { get; set; } = 0f;

	[ConVar( "sk_miniturret_health2" )]
	private static float SkMiniturretHealth2 { get; set; } = 0f;

	[ConVar( "sk_miniturret_health3" )]
	private static float SkMiniturretHealth3 { get; set; } = 0f;

	[ConVar( "sk_sentry_health1" )]
	private static float SkSentryHealth1 { get; set; } = 0f;

	[ConVar( "sk_sentry_health2" )]
	private static float SkSentryHealth2 { get; set; } = 0f;

	[ConVar( "sk_sentry_health3" )]
	private static float SkSentryHealth3 { get; set; } = 0f;

	[ConVar( "sk_plr_crowbar1" )]
	private static float SkPlrCrowbar1 { get; set; } = 0f;

	[ConVar( "sk_plr_crowbar2" )]
	private static float SkPlrCrowbar2 { get; set; } = 0f;

	[ConVar( "sk_plr_crowbar3" )]
	private static float SkPlrCrowbar3 { get; set; } = 0f;

	[ConVar( "sk_plr_9mm_bullet1" )]
	private static float SkPlr9mmBullet1 { get; set; } = 0f;

	[ConVar( "sk_plr_9mm_bullet2" )]
	private static float SkPlr9mmBullet2 { get; set; } = 0f;

	[ConVar( "sk_plr_9mm_bullet3" )]
	private static float SkPlr9mmBullet3 { get; set; } = 0f;

	[ConVar( "sk_plr_357_bullet1" )]
	private static float SkPlr357Bullet1 { get; set; } = 0f;

	[ConVar( "sk_plr_357_bullet2" )]
	private static float SkPlr357Bullet2 { get; set; } = 0f;

	[ConVar( "sk_plr_357_bullet3" )]
	private static float SkPlr357Bullet3 { get; set; } = 0f;

	[ConVar( "sk_plr_9mmAR_bullet1" )]
	private static float SkPlr9mmARBullet1 { get; set; } = 0f;

	[ConVar( "sk_plr_9mmAR_bullet2" )]
	private static float SkPlr9mmARBullet2 { get; set; } = 0f;

	[ConVar( "sk_plr_9mmAR_bullet3" )]
	private static float SkPlr9mmARBullet3 { get; set; } = 0f;

	[ConVar( "sk_plr_9mmAR_grenade1" )]
	private static float SkPlr9mmARGrenade1 { get; set; } = 0f;

	[ConVar( "sk_plr_9mmAR_grenade2" )]
	private static float SkPlr9mmARGrenade2 { get; set; } = 0f;

	[ConVar( "sk_plr_9mmAR_grenade3" )]
	private static float SkPlr9mmARGrenade3 { get; set; } = 0f;

	[ConVar( "sk_plr_buckshot1" )]
	private static float SkPlrBuckshot1 { get; set; } = 0f;

	[ConVar( "sk_plr_buckshot2" )]
	private static float SkPlrBuckshot2 { get; set; } = 0f;

	[ConVar( "sk_plr_buckshot3" )]
	private static float SkPlrBuckshot3 { get; set; } = 0f;

	[ConVar( "sk_plr_xbow_bolt_monster1" )]
	private static float SkPlrXbowBoltMonster1 { get; set; } = 0f;

	[ConVar( "sk_plr_xbow_bolt_monster2" )]
	private static float SkPlrXbowBoltMonster2 { get; set; } = 0f;

	[ConVar( "sk_plr_xbow_bolt_monster3" )]
	private static float SkPlrXbowBoltMonster3 { get; set; } = 0f;

	[ConVar( "sk_plr_xbow_bolt_client1" )]
	private static float SkPlrXbowBoltClient1 { get; set; } = 0f;

	[ConVar( "sk_plr_xbow_bolt_client2" )]
	private static float SkPlrXbowBoltClient2 { get; set; } = 0f;

	[ConVar( "sk_plr_xbow_bolt_client3" )]
	private static float SkPlrXbowBoltClient3 { get; set; } = 0f;

	[ConVar( "sk_plr_rpg1" )]
	private static float SkPlrRpg1 { get; set; } = 0f;

	[ConVar( "sk_plr_rpg2" )]
	private static float SkPlrRpg2 { get; set; } = 0f;

	[ConVar( "sk_plr_rpg3" )]
	private static float SkPlrRpg3 { get; set; } = 0f;

	[ConVar( "sk_plr_gauss1" )]
	private static float SkPlrGauss1 { get; set; } = 0f;

	[ConVar( "sk_plr_gauss2" )]
	private static float SkPlrGauss2 { get; set; } = 0f;

	[ConVar( "sk_plr_gauss3" )]
	private static float SkPlrGauss3 { get; set; } = 0f;

	[ConVar( "sk_plr_egon_narrow1" )]
	private static float SkPlrEgonNarrow1 { get; set; } = 0f;

	[ConVar( "sk_plr_egon_narrow2" )]
	private static float SkPlrEgonNarrow2 { get; set; } = 0f;

	[ConVar( "sk_plr_egon_narrow3" )]
	private static float SkPlrEgonNarrow3 { get; set; } = 0f;

	[ConVar( "sk_plr_egon_wide1" )]
	private static float SkPlrEgonWide1 { get; set; } = 0f;

	[ConVar( "sk_plr_egon_wide2" )]
	private static float SkPlrEgonWide2 { get; set; } = 0f;

	[ConVar( "sk_plr_egon_wide3" )]
	private static float SkPlrEgonWide3 { get; set; } = 0f;

	[ConVar( "sk_plr_hand_grenade1" )]
	private static float SkPlrHandGrenade1 { get; set; } = 0f;

	[ConVar( "sk_plr_hand_grenade2" )]
	private static float SkPlrHandGrenade2 { get; set; } = 0f;

	[ConVar( "sk_plr_hand_grenade3" )]
	private static float SkPlrHandGrenade3 { get; set; } = 0f;

	[ConVar( "sk_plr_satchel1" )]
	private static float SkPlrSatchel1 { get; set; } = 0f;

	[ConVar( "sk_plr_satchel2" )]
	private static float SkPlrSatchel2 { get; set; } = 0f;

	[ConVar( "sk_plr_satchel3" )]
	private static float SkPlrSatchel3 { get; set; } = 0f;

	[ConVar( "sk_plr_tripmine1" )]
	private static float SkPlrTripmine1 { get; set; } = 0f;

	[ConVar( "sk_plr_tripmine2" )]
	private static float SkPlrTripmine2 { get; set; } = 0f;

	[ConVar( "sk_plr_tripmine3" )]
	private static float SkPlrTripmine3 { get; set; } = 0f;

	[ConVar( "sk_12mm_bullet1" )]
	private static float Sk12mmBullet1 { get; set; } = 0f;

	[ConVar( "sk_12mm_bullet2" )]
	private static float Sk12mmBullet2 { get; set; } = 0f;

	[ConVar( "sk_12mm_bullet3" )]
	private static float Sk12mmBullet3 { get; set; } = 0f;

	[ConVar( "sk_9mmAR_bullet1" )]
	private static float Sk9mmARBullet1 { get; set; } = 0f;

	[ConVar( "sk_9mmAR_bullet2" )]
	private static float Sk9mmARBullet2 { get; set; } = 0f;

	[ConVar( "sk_9mmAR_bullet3" )]
	private static float Sk9mmARBullet3 { get; set; } = 0f;

	[ConVar( "sk_9mm_bullet1" )]
	private static float Sk9mmBullet1 { get; set; } = 0f;

	[ConVar( "sk_9mm_bullet2" )]
	private static float Sk9mmBullet2 { get; set; } = 0f;

	[ConVar( "sk_9mm_bullet3" )]
	private static float Sk9mmBullet3 { get; set; } = 0f;

	[ConVar( "sk_hornet_dmg1" )]
	private static float SkHornetDmg1 { get; set; } = 0f;

	[ConVar( "sk_hornet_dmg2" )]
	private static float SkHornetDmg2 { get; set; } = 0f;

	[ConVar( "sk_hornet_dmg3" )]
	private static float SkHornetDmg3 { get; set; } = 0f;

	[ConVar( "sk_suitcharger1" )]
	private static float SkSuitcharger1 { get; set; } = 0f;

	[ConVar( "sk_suitcharger2" )]
	private static float SkSuitcharger2 { get; set; } = 0f;

	[ConVar( "sk_suitcharger3" )]
	private static float SkSuitcharger3 { get; set; } = 0f;

	[ConVar( "sk_battery1" )]
	private static float SkBattery1 { get; set; } = 0f;

	[ConVar( "sk_battery2" )]
	private static float SkBattery2 { get; set; } = 0f;

	[ConVar( "sk_battery3" )]
	private static float SkBattery3 { get; set; } = 0f;

	[ConVar( "sk_healthcharger1" )]
	private static float SkHealthcharger1 { get; set; } = 0f;

	[ConVar( "sk_healthcharger2" )]
	private static float SkHealthcharger2 { get; set; } = 0f;

	[ConVar( "sk_healthcharger3" )]
	private static float SkHealthcharger3 { get; set; } = 0f;

	[ConVar( "sk_healthkit1" )]
	private static float SkHealthkit1 { get; set; } = 0f;

	[ConVar( "sk_healthkit2" )]
	private static float SkHealthkit2 { get; set; } = 0f;

	[ConVar( "sk_healthkit3" )]
	private static float SkHealthkit3 { get; set; } = 0f;

	[ConVar( "sk_scientist_heal1" )]
	private static float SkScientistHeal1 { get; set; } = 0f;

	[ConVar( "sk_scientist_heal2" )]
	private static float SkScientistHeal2 { get; set; } = 0f;

	[ConVar( "sk_scientist_heal3" )]
	private static float SkScientistHeal3 { get; set; } = 0f;

	[ConVar( "sk_monster_head1" )]
	private static float SkMonsterHead1 { get; set; } = 2f;

	[ConVar( "sk_monster_head2" )]
	private static float SkMonsterHead2 { get; set; } = 2f;

	[ConVar( "sk_monster_head3" )]
	private static float SkMonsterHead3 { get; set; } = 2f;

	[ConVar( "sk_monster_chest1" )]
	private static float SkMonsterChest1 { get; set; } = 1f;

	[ConVar( "sk_monster_chest2" )]
	private static float SkMonsterChest2 { get; set; } = 1f;

	[ConVar( "sk_monster_chest3" )]
	private static float SkMonsterChest3 { get; set; } = 1f;

	[ConVar( "sk_monster_stomach1" )]
	private static float SkMonsterStomach1 { get; set; } = 1f;

	[ConVar( "sk_monster_stomach2" )]
	private static float SkMonsterStomach2 { get; set; } = 1f;

	[ConVar( "sk_monster_stomach3" )]
	private static float SkMonsterStomach3 { get; set; } = 1f;

	[ConVar( "sk_monster_arm1" )]
	private static float SkMonsterArm1 { get; set; } = 1f;

	[ConVar( "sk_monster_arm2" )]
	private static float SkMonsterArm2 { get; set; } = 1f;

	[ConVar( "sk_monster_arm3" )]
	private static float SkMonsterArm3 { get; set; } = 1f;

	[ConVar( "sk_monster_leg1" )]
	private static float SkMonsterLeg1 { get; set; } = 1f;

	[ConVar( "sk_monster_leg2" )]
	private static float SkMonsterLeg2 { get; set; } = 1f;

	[ConVar( "sk_monster_leg3" )]
	private static float SkMonsterLeg3 { get; set; } = 1f;

	[ConVar( "sk_player_head1" )]
	private static float SkPlayerHead1 { get; set; } = 2f;

	[ConVar( "sk_player_head2" )]
	private static float SkPlayerHead2 { get; set; } = 2f;

	[ConVar( "sk_player_head3" )]
	private static float SkPlayerHead3 { get; set; } = 2f;

	[ConVar( "sk_player_chest1" )]
	private static float SkPlayerChest1 { get; set; } = 1f;

	[ConVar( "sk_player_chest2" )]
	private static float SkPlayerChest2 { get; set; } = 1f;

	[ConVar( "sk_player_chest3" )]
	private static float SkPlayerChest3 { get; set; } = 1f;

	[ConVar( "sk_player_stomach1" )]
	private static float SkPlayerStomach1 { get; set; } = 1f;

	[ConVar( "sk_player_stomach2" )]
	private static float SkPlayerStomach2 { get; set; } = 1f;

	[ConVar( "sk_player_stomach3" )]
	private static float SkPlayerStomach3 { get; set; } = 1f;

	[ConVar( "sk_player_arm1" )]
	private static float SkPlayerArm1 { get; set; } = 1f;

	[ConVar( "sk_player_arm2" )]
	private static float SkPlayerArm2 { get; set; } = 1f;

	[ConVar( "sk_player_arm3" )]
	private static float SkPlayerArm3 { get; set; } = 1f;

	[ConVar( "sk_player_leg1" )]
	private static float SkPlayerLeg1 { get; set; } = 1f;

	[ConVar( "sk_player_leg2" )]
	private static float SkPlayerLeg2 { get; set; } = 1f;

	[ConVar( "sk_player_leg3" )]
	private static float SkPlayerLeg3 { get; set; } = 1f;

	[ConVar( "sv_pushable_fixed_tick_fudge" )]
	private static float SvPushableFixedTickFudge { get; set; } = 15f;
}
