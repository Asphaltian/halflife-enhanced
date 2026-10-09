using System;
using GoldSrc;

namespace HalfLife;

partial class BaseEntity
{
	private bool EntVarsKeyValue( string key, string value )
	{
		switch ( key.ToLowerInvariant() )
		{
			case "classname":
				ClassName = Server.AllocString( value );
				break;
			case "globalname":
				GlobalName = Server.AllocString( value );
				break;
			case "origin":
				Origin = Util.StringToVector( value );
				break;
			case "oldorigin":
				OldOrigin = Util.StringToVector( value );
				break;
			case "velocity":
				Velocity = Util.StringToVector( value );
				break;
			case "basevelocity":
				BaseVelocity = Util.StringToVector( value );
				break;
			case "movedir":
				MoveDir = Util.StringToVector( value );
				break;
			case "angles":
				Angles = Util.StringToVector( value );
				break;
			case "avelocity":
				AVelocity = Util.StringToVector( value );
				break;
			case "punchangle":
				PunchAngle = Util.StringToVector( value );
				break;
			case "v_angle":
				ViewAngle = Util.StringToVector( value );
				break;
			case "fixangle":
				FixAngle = FloatBits( value );
				break;
			case "idealpitch":
				IdealPitch = Util.Atof( value );
				break;
			case "pitch_speed":
				PitchSpeed = Util.Atof( value );
				break;
			case "ideal_yaw":
				IdealYaw = Util.Atof( value );
				break;
			case "yaw_speed":
				YawSpeed = Util.Atof( value );
				break;
			case "modelindex":
				ModelIndex = Util.Atoi( value );
				break;
			case "model":
				ModelName = Server.AllocString( value );
				break;
			case "viewmodel":
				ViewModel = Server.AllocString( value );
				break;
			case "weaponmodel":
				WeaponModel = Server.AllocString( value );
				break;
			case "absmin":
				AbsMin = Util.StringToVector( value );
				break;
			case "absmax":
				AbsMax = Util.StringToVector( value );
				break;
			case "mins":
				Mins = Util.StringToVector( value );
				break;
			case "maxs":
				Maxs = Util.StringToVector( value );
				break;
			case "size":
				Size = Util.StringToVector( value );
				break;
			case "ltime":
				LTime = Util.Atof( value );
				break;
			case "nextthink":
				NextThink = Util.Atof( value );
				break;
			case "solid":
				Solid = (Solid)Util.Atoi( value );
				break;
			case "movetype":
				MoveType = (MoveType)Util.Atoi( value );
				break;
			case "skin":
				Skin = Util.Atoi( value );
				break;
			case "body":
				Body = Util.Atoi( value );
				break;
			case "effects":
				Effects = (Effects)Util.Atoi( value );
				break;
			case "gravity":
				Gravity = Util.Atof( value );
				break;
			case "friction":
				Friction = Util.Atof( value );
				break;
			case "light_level":
				LightLevel = FloatBits( value );
				break;
			case "frame":
				Frame = Util.Atof( value );
				break;
			case "scale":
				Scale = Util.Atof( value );
				break;
			case "sequence":
				Sequence = Util.Atoi( value );
				break;
			case "animtime":
				AnimTime = Util.Atof( value );
				break;
			case "framerate":
				FrameRate = Util.Atof( value );
				break;
			case "controller":
				BitConverter.GetBytes( Util.Atoi( value ) ).CopyTo( Controller, 0 );
				break;
			case "blending":
				BitConverter.GetBytes( (short)Util.Atoi( value ) ).CopyTo( Blending, 0 );
				break;
			case "rendermode":
				RenderMode = (RenderMode)Util.Atoi( value );
				break;
			case "renderamt":
				RenderAmt = Util.Atof( value );
				break;
			case "rendercolor":
				RenderColor = Util.StringToVector( value );
				break;
			case "renderfx":
				RenderFx = (RenderFx)Util.Atoi( value );
				break;
			case "health":
				Health = Util.Atof( value );
				break;
			case "frags":
				Frags = Util.Atof( value );
				break;
			case "weapons":
				Weapons = Util.Atoi( value );
				break;
			case "takedamage":
				DamageMode = Util.Atof( value );
				break;
			case "deadflag":
				DeadFlag = FloatBits( value );
				break;
			case "view_ofs":
				ViewOffset = Util.StringToVector( value );
				break;
			case "button":
				Button = Util.Atoi( value );
				break;
			case "impulse":
				Impulse = Util.Atoi( value );
				break;
			case "spawnflags":
				SpawnFlags = Util.Atoi( value );
				break;
			case "flags":
				EntityFlags = (EntityFlags)FloatBits( value );
				break;
			case "colormap":
				Colormap = Util.Atoi( value );
				break;
			case "team":
				Team = Util.Atoi( value );
				break;
			case "max_health":
				MaxHealth = Util.Atof( value );
				break;
			case "teleport_time":
				TeleportTime = Util.Atof( value );
				break;
			case "armortype":
				ArmorType = Util.Atof( value );
				break;
			case "armorvalue":
				ArmorValue = Util.Atof( value );
				break;
			case "waterlevel":
				WaterLevel = Util.Atoi( value );
				break;
			case "watertype":
				WaterType = Util.Atoi( value );
				break;
			case "target":
				Target = Server.AllocString( value );
				break;
			case "targetname":
				TargetName = Server.AllocString( value );
				break;
			case "netname":
				NetName = Server.AllocString( value );
				break;
			case "message":
				Message = Server.AllocString( value );
				break;
			case "dmg_take":
				DamageTake = Util.Atof( value );
				break;
			case "dmg_save":
				DamageSave = Util.Atof( value );
				break;
			case "dmg":
				Damage = Util.Atof( value );
				break;
			case "dmgtime":
				DamageTime = Util.Atof( value );
				break;
			case "noise":
				Noise = Server.AllocString( value );
				break;
			case "noise1":
				Noise1 = Server.AllocString( value );
				break;
			case "noise2":
				Noise2 = Server.AllocString( value );
				break;
			case "noise3":
				Noise3 = Server.AllocString( value );
				break;
			case "speed":
				Speed = Util.Atof( value );
				break;
			case "air_finished":
				AirFinished = Util.Atof( value );
				break;
			case "pain_finished":
				PainFinished = Util.Atof( value );
				break;
			case "radsuit_finished":
				RadSuitFinished = Util.Atof( value );
				break;
			case "chain":
			case "dmg_inflictor":
			case "enemy":
			case "aiment":
			case "owner":
			case "groundentity":
				Util.Alert( AlertType.Error, "Bad field in entity!!" );
				break;
			default:
				return false;
		}

		return true;
	}

	private static int FloatBits( string value )
	{
		return BitConverter.SingleToInt32Bits( Util.Atof( value ) );
	}
}
