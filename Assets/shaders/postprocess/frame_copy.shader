HEADER
{
	Description = "Copies the frame as it stands";
}

MODES
{
	Default();
	Forward();
}

FEATURES
{
}

COMMON
{
	#include "postprocess/shared.hlsl"
}

struct VertexInput
{
	float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
	float2 vTexCoord : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
	float2 uv : TEXCOORD0;

	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs : SV_Position;
	#endif

	#if ( PROGRAM == VFX_PROGRAM_PS )
		float4 vPositionSs : SV_Position;
	#endif
};

VS
{
	PixelInput MainVs( VertexInput i )
	{
		PixelInput o;

		o.vPositionPs = float4( i.vPositionOs.xy, 0.0f, 1.0f );
		o.uv = i.vTexCoord;

		return o;
	}
}

PS
{
	#include "postprocess/common.hlsl"

	Texture2D g_tColorBuffer < Attribute( "ColorBuffer" ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		return float4( g_tColorBuffer.Load( int3( i.vPositionSs.xy - g_vViewportOffset, 0 ) ).rgb, 1.0 );
	}
}
