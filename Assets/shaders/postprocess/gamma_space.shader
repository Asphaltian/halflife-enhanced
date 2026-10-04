HEADER
{
	Description = "Converts a frame rendered in gamma space to linear";
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
	Texture2D g_tGammaBuffer < Attribute( "GammaBuffer" ); >;

	float3 GammaToLinear( float3 color )
	{
		float3 low = color / 12.92;
		float3 high = pow( ( color + 0.055 ) / 1.055, 2.4 );
		return lerp( low, high, step( 0.04045, color ) );
	}

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float4 color = g_tColorBuffer.Load( int3( i.vPositionSs.xy - g_vViewportOffset, 0 ) );
		float coverage = 1.0 - saturate( color.a );

		if ( coverage >= 1.0 )
			return float4( GammaToLinear( saturate( color.rgb ) ), 1.0 );

		if ( coverage <= 0.0 )
			return float4( color.rgb, 1.0 );

		float3 gamma = g_tGammaBuffer.Load( int3( i.vPositionSs.xy - g_vViewportOffset, 0 ) ).rgb;

		return float4( GammaToLinear( saturate( gamma / coverage ) ) * coverage + ( color.rgb - gamma ), 1.0 );
	}
}
