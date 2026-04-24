shader_type canvas_item;

// === ✨ 剑刃与剑身发光控制 ===
uniform vec3 blade_core : source_color = vec3(1.0, 0.98, 0.85); // 剑刃核心（亮白金）
uniform vec3 blade_halo : source_color = vec3(1.0, 0.82, 0.15); // 剑刃光晕（金黄）

uniform float cycle_speed = 3.5; 
uniform float glow_radius : hint_range(0.0, 10.0) = 4.0;
uniform float halo_intensity : hint_range(0.0, 5.0) = 1.8;

// 检测容差
const float EPSILON = 0.1;

// 辅助函数：精准颜色匹配
bool color_match(vec3 c1, vec3 c2) {
    return distance(c1, c2) < EPSILON;
}

void fragment() {
    vec4 tex_color = texture(TEXTURE, UV);
    vec4 final_color = tex_color;

    // 💓 呼吸脉动因子
    float pulse = 0.5 + 0.5 * ((sin(TIME * cycle_speed) + 1.0) * 0.5);

    // ==========================================
    // 第一步：多层锚点检测与着色
    // ==========================================
    if (tex_color.a > 0.1) {
        vec3 rgb = tex_color.rgb;

        // 1. 核心剑刃 (255, 0, 255) [cite: 263]
        if (color_match(rgb, vec3(1.0, 0.0, 1.0))) {
            final_color.rgb = mix(blade_halo, blade_core, pulse);
        }
        // 2. 剑身主色 (251, 29, 65) -> 鲜红色 [cite: 263]
        else if (color_match(rgb, vec3(251.0/255.0, 29.0/255.0, 65.0/255.0))) {
            // 在原色基础上增加 20%~50% 的脉冲亮度
            final_color.rgb = rgb * (1.1 + 0.4 * pulse);
        }
        // 3. 剑身暗部 (143, 40, 90) -> 深紫红 [cite: 263]
        else if (color_match(rgb, vec3(143.0/255.0, 40.0/255.0, 90.0/255.0))) {
            // 维持深色质感，仅提供微弱的呼吸感
            final_color.rgb = rgb * (1.05 + 0.2 * pulse);
        }
    }

    // ==========================================
    // 第二步：复合环境光晕 (Software Bloom)
    // ==========================================
    if (tex_color.a < 0.05 && glow_radius > 0.0) {
        float total_weight = 0.0;
        vec3 combined_halo_color = vec3(0.0);
        vec2 ps = TEXTURE_PIXEL_SIZE;
        const float MAX_SEARCH = 10.0;

        for (float x = -MAX_SEARCH; x <= MAX_SEARCH; x += 1.0) {
            if (abs(x) > glow_radius) continue;
            for (float y = -MAX_SEARCH; y <= MAX_SEARCH; y += 1.0) {
                if (abs(y) > glow_radius) continue;

                vec2 offset = vec2(x, y);
                float dist = length(offset);

                if (dist <= glow_radius && dist > 0.0) {
                    vec4 neighbor = texture(TEXTURE, UV + offset * ps);
                    if (neighbor.a > 0.5) {
                        vec3 n_rgb = neighbor.rgb;
                        float weight = 1.0 - (dist / glow_radius);
                        
                        // 根据邻域像素类型决定光晕颜色
                        if (color_match(n_rgb, vec3(1.0, 0.0, 1.0))) {
                            combined_halo_color += blade_halo * weight;
                            total_weight += weight;
                        } 
                        else if (color_match(n_rgb, vec3(251.0/255.0, 29.0/255.0, 65.0/255.0))) {
                            combined_halo_color += vec3(1.0, 0.1, 0.2) * weight * 0.6;
                            total_weight += weight;
                        }
                    }
                }
            }
        }

        if (total_weight > 0.0) {
            float alpha = clamp(total_weight * 0.12 * halo_intensity * pulse, 0.0, 1.0);
            final_color = vec4(combined_halo_color / total_weight, alpha);
        }
    }

    if (final_color.a < 0.01) discard;
    COLOR = final_color;
}