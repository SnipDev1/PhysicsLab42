#ifndef TEXTURE_LAB_INCLUDED
#define TEXTURE_LAB_INCLUDED

    struct box
    {
        float3 pos;
        float3 size; // extent size
    };

    box box_init(float3 pos, float3 size)
    {
        box v;
        v.pos = pos; 
        v.size = size; 
        return v;
    }

    float3 box_intersect(float3 ro, float3 rd, box b)
    {   
        // Guard against division by zero when ray is parallel to an axis
        float3 safe_rd = abs(rd) > 1e-6 ? rd : (rd >= 0.0 ? 1e-6 : -1e-6);
        float3 m = 1.0 / safe_rd;
        float3 n = m * (ro - b.pos);
        float3 k = abs(m) * b.size;
        float3 t1 = -n - k;
        float3 t2 = -n + k;
        float tN = max(max(t1.x, t1.y), t1.z);
        float tF = min(min(t2.x, t2.y), t2.z);
        return float3(tN, tF, min(tF - tN, tF)); 
    }

    float plane_intersect(float3 ro, float3 rd, float4 p)
    {
        float denom = dot(rd, p.xyz);
        if (abs(denom) < 1e-6) return -1.0;
        return -(dot(ro, p.xyz) + p.w) / denom;
    }

    float4 sub_texture_uv(float2 uv, float n, float m, float id, float scale=1)
    {
        id = round(id);
        float y = floor(id / n); 
        float x = floor(fmod(id, m));
        float unit_x = 1.0 / n;
        float unit_y = 1.0 / m;
        float2 offset_uv = float2(unit_x * x, unit_y * y);
        float2 iuv = offset_uv + uv * float2(unit_x, unit_y);
        return float4(iuv, offset_uv);
    }
    
    // Computes normal, atlas sub-texture id, and local wall UV in one consistent step to eliminate edge mismatches
    void get_wall_info(float3 p, out float3 nor, out float id, out float2 uv)
    {
        float3 abp = abs(p);
        if (abp.x >= abp.y && abp.x >= abp.z)
        {
            float sx = sign(p.x);
            nor = float3(sx, 0.0, 0.0);
            id = (sx > 0.0) ? 5.0 : 3.0; // Right wall (5) or Left wall (3)
            uv.x = (sx > 0.0) ? (0.5 - p.z) : (p.z + 0.5);
            uv.y = p.y + 0.5;
        }
        else if (abp.y >= abp.z)
        {
            float sy = sign(p.y);
            nor = float3(0.0, sy, 0.0);
            id = (sy > 0.0) ? 7.0 : 1.0; // Ceiling (7) or Floor (1)
            uv.x = p.x + 0.5;
            uv.y = (sy > 0.0) ? (0.5 - p.z) : (p.z + 0.5);
        }
        else
        {
            float sz = sign(p.z);
            nor = float3(0.0, 0.0, sz);
            id = (sz > 0.0) ? 4.0 : 0.0; // Back wall (4) or Front window (0)
            uv.x = p.x + 0.5;
            uv.y = p.y + 0.5;
        }
    }

    float3 get_normal(float3 p)
    {
        float3 nor; float id; float2 uv;
        get_wall_info(p, nor, id, uv);
        return nor;
    }

    float get_sub_tex_id(float3 nor)
    {
        if (nor.y == 1.0) return 7.0;
        if (nor.y == -1.0) return 1.0;
        if (nor.x == 1.0) return 5.0;
        if (nor.x == -1.0) return 3.0;
        if (nor.z == 1.0) return 4.0;
        return 0.0;
    }

    float2 get_pos_uv(float3 p)
    {
        float3 nor; float id; float2 uv;
        get_wall_info(p, nor, id, uv);
        return uv;
    }
    
#endif 