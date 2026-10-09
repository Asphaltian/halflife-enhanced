using System;
using System.Buffers.Binary;
using System.Text;

namespace GoldSrc;

internal static class StructImage
{
	public static int ReadInt( byte[] image, int offset ) => BinaryPrimitives.ReadInt32LittleEndian( image.AsSpan( offset ) );

	public static short ReadShort( byte[] image, int offset ) => BinaryPrimitives.ReadInt16LittleEndian( image.AsSpan( offset ) );

	public static float ReadFloat( byte[] image, int offset ) => BitConverter.Int32BitsToSingle( ReadInt( image, offset ) );

	public static Vector3 ReadVector( byte[] image, int offset )
	{
		return new Vector3( ReadFloat( image, offset ), ReadFloat( image, offset + 4 ), ReadFloat( image, offset + 8 ) );
	}

	public static void Write( byte[] image, int offset, int value ) => BinaryPrimitives.WriteInt32LittleEndian( image.AsSpan( offset ), value );

	public static void Write( byte[] image, int offset, short value ) => BinaryPrimitives.WriteInt16LittleEndian( image.AsSpan( offset ), value );

	public static void Write( byte[] image, int offset, float value ) => Write( image, offset, BitConverter.SingleToInt32Bits( value ) );

	public static void Write( byte[] image, int offset, Vector3 value )
	{
		Write( image, offset, value.x );
		Write( image, offset + 4, value.y );
		Write( image, offset + 8, value.z );
	}

	public static string ReadString( byte[] image, int offset, int size )
	{
		var length = 0;

		while ( length < size && image[offset + length] != 0 )
			length++;

		return Encoding.Latin1.GetString( image, offset, length );
	}

	public static void Write( byte[] image, int offset, int size, string value )
	{
		Array.Clear( image, offset, size );

		var bytes = Encoding.Latin1.GetBytes( value ?? "" );

		Array.Copy( bytes, 0, image, offset, Math.Min( bytes.Length, size - 1 ) );
	}
}

public struct EntityState
{
	public const int Size = 340;

	public const int EntityNormal = 1 << 0;
	public const int EntityBeam = 1 << 1;

	public const int NumberOffset = 4;
	public const int RenderModeOffset = 72;
	public const int MoveTypeOffset = 88;
	public const int AnimTimeOffset = 92;
	public const int AimEntOffset = 148;
	public const int ImpactTimeOffset = 252;
	public const int StartTimeOffset = 256;

	public const int EFlagSlerp = 1;
	public const int EFlagFleshSound = 2;

	public int EntityType;
	public int Number;
	public float MsgTime;
	public int MessageNum;

	public Vector3 Origin;
	public Vector3 Angles;

	public int ModelIndex;
	public int Sequence;
	public float Frame;
	public int Colormap;
	public short Skin;
	public short Solid;
	public int Effects;
	public float Scale;

	public byte EFlags;

	public int RenderMode;
	public int RenderAmt;
	public byte RenderColorR;
	public byte RenderColorG;
	public byte RenderColorB;
	public int RenderFx;

	public int MoveType;
	public float AnimTime;
	public float FrameRate;
	public int Body;
	public byte Controller0;
	public byte Controller1;
	public byte Controller2;
	public byte Controller3;
	public byte Blending0;
	public byte Blending1;
	public byte Blending2;
	public byte Blending3;
	public Vector3 Velocity;

	public Vector3 Mins;
	public Vector3 Maxs;

	public int AimEnt;
	public int Owner;

	public float Friction;
	public float Gravity;

	public int Team;
	public int PlayerClass;
	public int Health;
	public int Spectator;
	public int WeaponModel;
	public int GaitSequence;
	public Vector3 BaseVelocity;
	public int UseHull;
	public int OldButtons;
	public int OnGround;
	public int StepLeft;
	public float FallVelocity;

	public float Fov;
	public int WeaponAnim;

	public Vector3 StartPos;
	public Vector3 EndPos;
	public float ImpactTime;
	public float StartTime;

	public int IUser1;
	public int IUser2;
	public int IUser3;
	public int IUser4;
	public float FUser1;
	public float FUser2;
	public float FUser3;
	public float FUser4;
	public Vector3 VUser1;
	public Vector3 VUser2;
	public Vector3 VUser3;
	public Vector3 VUser4;

	public readonly byte GetController( int index )
	{
		return index switch
		{
			0 => Controller0,
			1 => Controller1,
			2 => Controller2,
			3 => Controller3,
			4 => Blending0,
			5 => Blending1,
			6 => Blending2,
			_ => Blending3
		};
	}

	public readonly void Write( byte[] image )
	{
		Array.Clear( image, 0, Size );

		StructImage.Write( image, 0, EntityType );
		StructImage.Write( image, NumberOffset, Number );
		StructImage.Write( image, 8, MsgTime );
		StructImage.Write( image, 12, MessageNum );
		StructImage.Write( image, 16, Origin );
		StructImage.Write( image, 28, Angles );
		StructImage.Write( image, 40, ModelIndex );
		StructImage.Write( image, 44, Sequence );
		StructImage.Write( image, 48, Frame );
		StructImage.Write( image, 52, Colormap );
		StructImage.Write( image, 56, Skin );
		StructImage.Write( image, 58, Solid );
		StructImage.Write( image, 60, Effects );
		StructImage.Write( image, 64, Scale );
		image[68] = EFlags;
		StructImage.Write( image, RenderModeOffset, RenderMode );
		StructImage.Write( image, 76, RenderAmt );
		image[80] = RenderColorR;
		image[81] = RenderColorG;
		image[82] = RenderColorB;
		StructImage.Write( image, 84, RenderFx );
		StructImage.Write( image, MoveTypeOffset, MoveType );
		StructImage.Write( image, AnimTimeOffset, AnimTime );
		StructImage.Write( image, 96, FrameRate );
		StructImage.Write( image, 100, Body );
		image[104] = Controller0;
		image[105] = Controller1;
		image[106] = Controller2;
		image[107] = Controller3;
		image[108] = Blending0;
		image[109] = Blending1;
		image[110] = Blending2;
		image[111] = Blending3;
		StructImage.Write( image, 112, Velocity );
		StructImage.Write( image, 124, Mins );
		StructImage.Write( image, 136, Maxs );
		StructImage.Write( image, AimEntOffset, AimEnt );
		StructImage.Write( image, 152, Owner );
		StructImage.Write( image, 156, Friction );
		StructImage.Write( image, 160, Gravity );
		StructImage.Write( image, 164, Team );
		StructImage.Write( image, 168, PlayerClass );
		StructImage.Write( image, 172, Health );
		StructImage.Write( image, 176, Spectator );
		StructImage.Write( image, 180, WeaponModel );
		StructImage.Write( image, 184, GaitSequence );
		StructImage.Write( image, 188, BaseVelocity );
		StructImage.Write( image, 200, UseHull );
		StructImage.Write( image, 204, OldButtons );
		StructImage.Write( image, 208, OnGround );
		StructImage.Write( image, 212, StepLeft );
		StructImage.Write( image, 216, FallVelocity );
		StructImage.Write( image, 220, Fov );
		StructImage.Write( image, 224, WeaponAnim );
		StructImage.Write( image, 228, StartPos );
		StructImage.Write( image, 240, EndPos );
		StructImage.Write( image, ImpactTimeOffset, ImpactTime );
		StructImage.Write( image, StartTimeOffset, StartTime );
		StructImage.Write( image, 260, IUser1 );
		StructImage.Write( image, 264, IUser2 );
		StructImage.Write( image, 268, IUser3 );
		StructImage.Write( image, 272, IUser4 );
		StructImage.Write( image, 276, FUser1 );
		StructImage.Write( image, 280, FUser2 );
		StructImage.Write( image, 284, FUser3 );
		StructImage.Write( image, 288, FUser4 );
		StructImage.Write( image, 292, VUser1 );
		StructImage.Write( image, 304, VUser2 );
		StructImage.Write( image, 316, VUser3 );
		StructImage.Write( image, 328, VUser4 );
	}

	public static EntityState Read( byte[] image )
	{
		return new EntityState
		{
			EntityType = StructImage.ReadInt( image, 0 ),
			Number = StructImage.ReadInt( image, NumberOffset ),
			MsgTime = StructImage.ReadFloat( image, 8 ),
			MessageNum = StructImage.ReadInt( image, 12 ),
			Origin = StructImage.ReadVector( image, 16 ),
			Angles = StructImage.ReadVector( image, 28 ),
			ModelIndex = StructImage.ReadInt( image, 40 ),
			Sequence = StructImage.ReadInt( image, 44 ),
			Frame = StructImage.ReadFloat( image, 48 ),
			Colormap = StructImage.ReadInt( image, 52 ),
			Skin = StructImage.ReadShort( image, 56 ),
			Solid = StructImage.ReadShort( image, 58 ),
			Effects = StructImage.ReadInt( image, 60 ),
			Scale = StructImage.ReadFloat( image, 64 ),
			EFlags = image[68],
			RenderMode = StructImage.ReadInt( image, RenderModeOffset ),
			RenderAmt = StructImage.ReadInt( image, 76 ),
			RenderColorR = image[80],
			RenderColorG = image[81],
			RenderColorB = image[82],
			RenderFx = StructImage.ReadInt( image, 84 ),
			MoveType = StructImage.ReadInt( image, MoveTypeOffset ),
			AnimTime = StructImage.ReadFloat( image, AnimTimeOffset ),
			FrameRate = StructImage.ReadFloat( image, 96 ),
			Body = StructImage.ReadInt( image, 100 ),
			Controller0 = image[104],
			Controller1 = image[105],
			Controller2 = image[106],
			Controller3 = image[107],
			Blending0 = image[108],
			Blending1 = image[109],
			Blending2 = image[110],
			Blending3 = image[111],
			Velocity = StructImage.ReadVector( image, 112 ),
			Mins = StructImage.ReadVector( image, 124 ),
			Maxs = StructImage.ReadVector( image, 136 ),
			AimEnt = StructImage.ReadInt( image, AimEntOffset ),
			Owner = StructImage.ReadInt( image, 152 ),
			Friction = StructImage.ReadFloat( image, 156 ),
			Gravity = StructImage.ReadFloat( image, 160 ),
			Team = StructImage.ReadInt( image, 164 ),
			PlayerClass = StructImage.ReadInt( image, 168 ),
			Health = StructImage.ReadInt( image, 172 ),
			Spectator = StructImage.ReadInt( image, 176 ),
			WeaponModel = StructImage.ReadInt( image, 180 ),
			GaitSequence = StructImage.ReadInt( image, 184 ),
			BaseVelocity = StructImage.ReadVector( image, 188 ),
			UseHull = StructImage.ReadInt( image, 200 ),
			OldButtons = StructImage.ReadInt( image, 204 ),
			OnGround = StructImage.ReadInt( image, 208 ),
			StepLeft = StructImage.ReadInt( image, 212 ),
			FallVelocity = StructImage.ReadFloat( image, 216 ),
			Fov = StructImage.ReadFloat( image, 220 ),
			WeaponAnim = StructImage.ReadInt( image, 224 ),
			StartPos = StructImage.ReadVector( image, 228 ),
			EndPos = StructImage.ReadVector( image, 240 ),
			ImpactTime = StructImage.ReadFloat( image, ImpactTimeOffset ),
			StartTime = StructImage.ReadFloat( image, StartTimeOffset ),
			IUser1 = StructImage.ReadInt( image, 260 ),
			IUser2 = StructImage.ReadInt( image, 264 ),
			IUser3 = StructImage.ReadInt( image, 268 ),
			IUser4 = StructImage.ReadInt( image, 272 ),
			FUser1 = StructImage.ReadFloat( image, 276 ),
			FUser2 = StructImage.ReadFloat( image, 280 ),
			FUser3 = StructImage.ReadFloat( image, 284 ),
			FUser4 = StructImage.ReadFloat( image, 288 ),
			VUser1 = StructImage.ReadVector( image, 292 ),
			VUser2 = StructImage.ReadVector( image, 304 ),
			VUser3 = StructImage.ReadVector( image, 316 ),
			VUser4 = StructImage.ReadVector( image, 328 )
		};
	}
}

public struct LocalClientData
{
	public const int Size = 476;
	public const int MaxPhysInfoString = 256;

	public Vector3 Origin;
	public Vector3 Velocity;

	public int ViewModel;
	public Vector3 PunchAngle;
	public int Flags;
	public int WaterLevel;
	public int WaterType;
	public Vector3 ViewOffset;
	public float Health;

	public int InDuck;

	public int Weapons;

	public int TimeStepSound;
	public int DuckTime;
	public int SwimTime;
	public int WaterJumpTime;

	public float MaxSpeed;

	public float Fov;
	public int WeaponAnim;

	public int Id;
	public int AmmoShells;
	public int AmmoNails;
	public int AmmoCells;
	public int AmmoRockets;
	public float NextAttack;

	public int TfState;

	public int PushMsec;

	public int DeadFlag;

	public string PhysInfo;

	public int IUser1;
	public int IUser2;
	public int IUser3;
	public int IUser4;
	public float FUser1;
	public float FUser2;
	public float FUser3;
	public float FUser4;
	public Vector3 VUser1;
	public Vector3 VUser2;
	public Vector3 VUser3;
	public Vector3 VUser4;

	public readonly void Write( byte[] image )
	{
		StructImage.Write( image, 0, Origin );
		StructImage.Write( image, 12, Velocity );
		StructImage.Write( image, 24, ViewModel );
		StructImage.Write( image, 28, PunchAngle );
		StructImage.Write( image, 40, Flags );
		StructImage.Write( image, 44, WaterLevel );
		StructImage.Write( image, 48, WaterType );
		StructImage.Write( image, 52, ViewOffset );
		StructImage.Write( image, 64, Health );
		StructImage.Write( image, 68, InDuck );
		StructImage.Write( image, 72, Weapons );
		StructImage.Write( image, 76, TimeStepSound );
		StructImage.Write( image, 80, DuckTime );
		StructImage.Write( image, 84, SwimTime );
		StructImage.Write( image, 88, WaterJumpTime );
		StructImage.Write( image, 92, MaxSpeed );
		StructImage.Write( image, 96, Fov );
		StructImage.Write( image, 100, WeaponAnim );
		StructImage.Write( image, 104, Id );
		StructImage.Write( image, 108, AmmoShells );
		StructImage.Write( image, 112, AmmoNails );
		StructImage.Write( image, 116, AmmoCells );
		StructImage.Write( image, 120, AmmoRockets );
		StructImage.Write( image, 124, NextAttack );
		StructImage.Write( image, 128, TfState );
		StructImage.Write( image, 132, PushMsec );
		StructImage.Write( image, 136, DeadFlag );
		StructImage.Write( image, 140, MaxPhysInfoString, PhysInfo );
		StructImage.Write( image, 396, IUser1 );
		StructImage.Write( image, 400, IUser2 );
		StructImage.Write( image, 404, IUser3 );
		StructImage.Write( image, 408, IUser4 );
		StructImage.Write( image, 412, FUser1 );
		StructImage.Write( image, 416, FUser2 );
		StructImage.Write( image, 420, FUser3 );
		StructImage.Write( image, 424, FUser4 );
		StructImage.Write( image, 428, VUser1 );
		StructImage.Write( image, 440, VUser2 );
		StructImage.Write( image, 452, VUser3 );
		StructImage.Write( image, 464, VUser4 );
	}

	public static LocalClientData Read( byte[] image )
	{
		return new LocalClientData
		{
			Origin = StructImage.ReadVector( image, 0 ),
			Velocity = StructImage.ReadVector( image, 12 ),
			ViewModel = StructImage.ReadInt( image, 24 ),
			PunchAngle = StructImage.ReadVector( image, 28 ),
			Flags = StructImage.ReadInt( image, 40 ),
			WaterLevel = StructImage.ReadInt( image, 44 ),
			WaterType = StructImage.ReadInt( image, 48 ),
			ViewOffset = StructImage.ReadVector( image, 52 ),
			Health = StructImage.ReadFloat( image, 64 ),
			InDuck = StructImage.ReadInt( image, 68 ),
			Weapons = StructImage.ReadInt( image, 72 ),
			TimeStepSound = StructImage.ReadInt( image, 76 ),
			DuckTime = StructImage.ReadInt( image, 80 ),
			SwimTime = StructImage.ReadInt( image, 84 ),
			WaterJumpTime = StructImage.ReadInt( image, 88 ),
			MaxSpeed = StructImage.ReadFloat( image, 92 ),
			Fov = StructImage.ReadFloat( image, 96 ),
			WeaponAnim = StructImage.ReadInt( image, 100 ),
			Id = StructImage.ReadInt( image, 104 ),
			AmmoShells = StructImage.ReadInt( image, 108 ),
			AmmoNails = StructImage.ReadInt( image, 112 ),
			AmmoCells = StructImage.ReadInt( image, 116 ),
			AmmoRockets = StructImage.ReadInt( image, 120 ),
			NextAttack = StructImage.ReadFloat( image, 124 ),
			TfState = StructImage.ReadInt( image, 128 ),
			PushMsec = StructImage.ReadInt( image, 132 ),
			DeadFlag = StructImage.ReadInt( image, 136 ),
			PhysInfo = StructImage.ReadString( image, 140, MaxPhysInfoString ),
			IUser1 = StructImage.ReadInt( image, 396 ),
			IUser2 = StructImage.ReadInt( image, 400 ),
			IUser3 = StructImage.ReadInt( image, 404 ),
			IUser4 = StructImage.ReadInt( image, 408 ),
			FUser1 = StructImage.ReadFloat( image, 412 ),
			FUser2 = StructImage.ReadFloat( image, 416 ),
			FUser3 = StructImage.ReadFloat( image, 420 ),
			FUser4 = StructImage.ReadFloat( image, 424 ),
			VUser1 = StructImage.ReadVector( image, 428 ),
			VUser2 = StructImage.ReadVector( image, 440 ),
			VUser3 = StructImage.ReadVector( image, 452 ),
			VUser4 = StructImage.ReadVector( image, 464 )
		};
	}
}

public struct WeaponData
{
	public const int Size = 88;

	public int Id;
	public int Clip;

	public float NextPrimaryAttack;
	public float NextSecondaryAttack;
	public float TimeWeaponIdle;

	public int InReload;
	public int InSpecialReload;
	public float NextReload;
	public float PumpTime;
	public float ReloadTime;

	public float AimedDamage;
	public float NextAimBonus;
	public int InZoom;
	public int WeaponState;

	public int IUser1;
	public int IUser2;
	public int IUser3;
	public int IUser4;
	public float FUser1;
	public float FUser2;
	public float FUser3;
	public float FUser4;

	public readonly void Write( byte[] image )
	{
		StructImage.Write( image, 0, Id );
		StructImage.Write( image, 4, Clip );
		StructImage.Write( image, 8, NextPrimaryAttack );
		StructImage.Write( image, 12, NextSecondaryAttack );
		StructImage.Write( image, 16, TimeWeaponIdle );
		StructImage.Write( image, 20, InReload );
		StructImage.Write( image, 24, InSpecialReload );
		StructImage.Write( image, 28, NextReload );
		StructImage.Write( image, 32, PumpTime );
		StructImage.Write( image, 36, ReloadTime );
		StructImage.Write( image, 40, AimedDamage );
		StructImage.Write( image, 44, NextAimBonus );
		StructImage.Write( image, 48, InZoom );
		StructImage.Write( image, 52, WeaponState );
		StructImage.Write( image, 56, IUser1 );
		StructImage.Write( image, 60, IUser2 );
		StructImage.Write( image, 64, IUser3 );
		StructImage.Write( image, 68, IUser4 );
		StructImage.Write( image, 72, FUser1 );
		StructImage.Write( image, 76, FUser2 );
		StructImage.Write( image, 80, FUser3 );
		StructImage.Write( image, 84, FUser4 );
	}

	public static WeaponData Read( byte[] image )
	{
		return new WeaponData
		{
			Id = StructImage.ReadInt( image, 0 ),
			Clip = StructImage.ReadInt( image, 4 ),
			NextPrimaryAttack = StructImage.ReadFloat( image, 8 ),
			NextSecondaryAttack = StructImage.ReadFloat( image, 12 ),
			TimeWeaponIdle = StructImage.ReadFloat( image, 16 ),
			InReload = StructImage.ReadInt( image, 20 ),
			InSpecialReload = StructImage.ReadInt( image, 24 ),
			NextReload = StructImage.ReadFloat( image, 28 ),
			PumpTime = StructImage.ReadFloat( image, 32 ),
			ReloadTime = StructImage.ReadFloat( image, 36 ),
			AimedDamage = StructImage.ReadFloat( image, 40 ),
			NextAimBonus = StructImage.ReadFloat( image, 44 ),
			InZoom = StructImage.ReadInt( image, 48 ),
			WeaponState = StructImage.ReadInt( image, 52 ),
			IUser1 = StructImage.ReadInt( image, 56 ),
			IUser2 = StructImage.ReadInt( image, 60 ),
			IUser3 = StructImage.ReadInt( image, 64 ),
			IUser4 = StructImage.ReadInt( image, 68 ),
			FUser1 = StructImage.ReadFloat( image, 72 ),
			FUser2 = StructImage.ReadFloat( image, 76 ),
			FUser3 = StructImage.ReadFloat( image, 80 ),
			FUser4 = StructImage.ReadFloat( image, 84 )
		};
	}
}
