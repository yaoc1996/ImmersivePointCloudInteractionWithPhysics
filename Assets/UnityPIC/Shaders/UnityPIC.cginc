#ifndef RENDERER_PLUGIN_POINT_CGINC
#define RENDERER_PLUGIN_POINT_CGINC

#define MAX_PIXELSIZE_FACTOR 12.0f
#define MIN_PIXELSIZE_FACTOR 1.0f
#define BLEND_DEPTH_FACTOR 0.01f

#define ASPECT_RATIO _ScreenParams.x * frac(_ScreenParams.w)

#define DEFAULT_POINT_POS_UV_TO_STREAM(o, vertex, point_uv, ostream, radius) \
float pixel_size = 2 * o . vertex .w * frac(_ScreenParams.z);\
float a = 2 * min(max(abs(radius * (UNITY_MATRIX_P[0][0] + UNITY_MATRIX_P[0][1])), MIN_PIXELSIZE_FACTOR * pixel_size), MAX_PIXELSIZE_FACTOR * pixel_size);\
float b = a * ASPECT_RATIO;\
OUTPUT_POINT_POS_UV_TO_STREAM(o, vertex, point_uv, ostream, a, b)

#define OUTPUT_POINT_POS_UV_TO_STREAM(o, vertex, point_uv, ostream, a, b) \
o . vertex .x -= a * 0.5f;\
o . vertex .y += b * 0.5f;\
o . point_uv  = float2(-1.0f, -1.0f);\
ostream .Append(o);\
o . vertex .y -= b;\
o . point_uv .y += 2.0f;\
ostream .Append(o);\
o . vertex .x += a;\
o . vertex .y += b;\
o . point_uv .y -= 2.0f;\
o . point_uv .x += 2.0f;\
ostream .Append(o);\
o . vertex .y -= b;\
o . point_uv .y += 2.0f;\
ostream .Append(o)

#define DEFAULT_POINT_POS_TO_STREAM(o, vertex, ostream, radius) \
float pixel_size = 2 * o . vertex .w * frac(_ScreenParams.z);\
float a = 2 * min(max(abs(radius * (UNITY_MATRIX_P[0][0] + UNITY_MATRIX_P[0][1])), MIN_PIXELSIZE_FACTOR * pixel_size), MAX_PIXELSIZE_FACTOR * pixel_size);\
float b = a * ASPECT_RATIO;\
OUTPUT_POINT_POS_TO_STREAM(o, vertex, ostream, a, b)

#define OUTPUT_POINT_POS_TO_STREAM(o, vertex, ostream, a, b) \
o . vertex .x -= a * 0.5f;\
o . vertex .y += b * 0.5f;\
ostream .Append(o);\
o . vertex .y -= b;\
ostream .Append(o);\
o . vertex .x += a;\
o . vertex .y += b;\
ostream .Append(o);\
o . vertex .y -= b;\
ostream .Append(o)

struct PosCol16 {
	float3 vertex : POSITION;
	uint color;

	inline fixed4 get_color() {
		fixed4 c;
		c.r = (color & 0xFF) / 255.0f;
		c.g = ((color >> 8) & 0xFF) / 255.0f;
		c.b = ((color >> 16) & 0xFF) / 255.0f;
		c.a = ((color >> 24) & 0xFF) / 255.0f;

		return c;
	}
};

struct PosColIntCls20
{
	float3 vertex : POSITION;
	uint col;
	uint attributes;

	inline fixed4 color() {
		fixed4 c;
		c.r = (col & 0xFF) / 255.0f;
		c.g = ((col >> 8) & 0xFF) / 255.0f;
		c.b = ((col >> 16) & 0xFF) / 255.0f;
		c.a = ((col >> 24) & 0xFF) / 255.0f;

		return c;
	}

	inline half intensity() {
		static const uint mask = 0xFFFF;
		static const half unit = 1.0f / ((uint)0xFFFF);
		return (attributes & mask) * unit;
	}

	inline fixed classification() {
		static const uint mask = 0xFF;
		static const fixed unit = 1.0f / ((uint)0xFF);
		return ((attributes >> 16) & mask) * unit;
	}
};

inline fixed4 decodeUintToRGBA(uint col) {
	static const uint mask = 0xFF;
	static const fixed unit = 1.0f / ((uint)0xFF);

	return fixed4((mask & col) * unit, (mask & (col >> 8)) * unit, (mask & (col >> 16)) * unit, (mask & (col >> 24)) * unit);
}

inline uint encodeRGBAToUint(fixed4 col) {
	static const uint mask = 0xFF;

	return ((uint)(col.r * mask)) | (((uint)(col.g * mask)) << 8) | (((uint)(col.b * mask)) << 16) | (((uint)(col.a * mask)) << 24);
}

inline float scale_radius(float rad, float dep) {
	return rad + dep * 128;// *512;
}

inline float depth(float z) {
	return ((1.0f / z) - _ZBufferParams.y) / _ZBufferParams.x;
}

inline float linear_depth(float z) {
	return 1.0f / (z * _ZBufferParams.x + _ZBufferParams.y);
}

inline float l2_norm(float3 p1, float3 p2) {
	return sqrt(pow(p1.x - p2.x, 2) + pow(p1.y - p2.y, 2) + pow(p1.z - p2.z, 2));
}

float normalize_intensity(float intensity, float min, float max) {
	return (intensity - min) / (max - min);
}

fixed3 hsv_to_rgb(fixed3 HSV)
{
	fixed3 RGB = HSV.z;

	fixed var_h = HSV.x * 6;
	fixed var_i = floor(var_h);   // Or ... var_i = floor( var_h )
	fixed var_1 = HSV.z * (1.0 - HSV.y);
	fixed var_2 = HSV.z * (1.0 - HSV.y * (var_h-var_i));
	fixed var_3 = HSV.z * (1.0 - HSV.y * (1-(var_h-var_i)));
	if      (var_i == 0) { RGB = fixed3(HSV.z, var_3, var_1); }
	else if (var_i == 1) { RGB = fixed3(var_2, HSV.z, var_1); }
	else if (var_i == 2) { RGB = fixed3(var_1, HSV.z, var_3); }
	else if (var_i == 3) { RGB = fixed3(var_1, var_2, HSV.z); }
	else if (var_i == 4) { RGB = fixed3(var_3, var_1, HSV.z); }
	else                 { RGB = fixed3(HSV.z, var_1, var_2); }

   return RGB;
}

inline uint partition_id(float3 pos, float3 center) {
	return 1 << (((pos.x > center.x) * (1 << 2)) | ((pos.y > center.y) * (1 << 0)) | ((pos.z > center.z) * (1 << 1)));
}

#endif