using System;

namespace GoldSrc;

public struct UserCmd
{
	public const int Size = 52;

	public short LerpMsec;
	public byte Msec;
	public Vector3 ViewAngles;
	public float ForwardMove;
	public float SideMove;
	public float UpMove;
	public byte LightLevel;
	public ushort Buttons;
	public byte Impulse;
	public byte WeaponSelect;
	public int ImpactIndex;
	public Vector3 ImpactPosition;

	public readonly void Write( byte[] image )
	{
		Array.Clear( image, 0, Size );

		StructImage.Write( image, 0, LerpMsec );
		image[2] = Msec;
		StructImage.Write( image, 4, ViewAngles );
		StructImage.Write( image, 16, ForwardMove );
		StructImage.Write( image, 20, SideMove );
		StructImage.Write( image, 24, UpMove );
		image[28] = LightLevel;
		StructImage.Write( image, 30, (short)Buttons );
		image[32] = Impulse;
		image[33] = WeaponSelect;
		StructImage.Write( image, 36, ImpactIndex );
		StructImage.Write( image, 40, ImpactPosition );
	}

	public static UserCmd Read( byte[] image )
	{
		return new UserCmd
		{
			LerpMsec = StructImage.ReadShort( image, 0 ),
			Msec = image[2],
			ViewAngles = StructImage.ReadVector( image, 4 ),
			ForwardMove = StructImage.ReadFloat( image, 16 ),
			SideMove = StructImage.ReadFloat( image, 20 ),
			UpMove = StructImage.ReadFloat( image, 24 ),
			LightLevel = image[28],
			Buttons = (ushort)StructImage.ReadShort( image, 30 ),
			Impulse = image[32],
			WeaponSelect = image[33],
			ImpactIndex = StructImage.ReadInt( image, 36 ),
			ImpactPosition = StructImage.ReadVector( image, 40 )
		};
	}
}
