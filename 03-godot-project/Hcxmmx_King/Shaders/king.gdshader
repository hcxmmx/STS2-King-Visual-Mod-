shader_type canvas_item;

uniform sampler2D palette_lut : filter_nearest;

// === ✨ 武器动态流光控制 ===
uniform vec3 glow_color_1 : source_color = vec3(1.0, 0.82, 0.2);
uniform vec3 glow_color_2 : source_color = vec3(0.2, 0.8, 1.0);
uniform float cycle_speed = 2.0;
uniform float glow_radius : hint_range(0.0, 10.0) = 2.0;
uniform float glow_intensity : hint_range(0.0, 10.0) = 2.0;

// === 🥷 本体柔光控制 ===
uniform vec3 body_glow_color : source_color = vec3(0.8, 0.9, 1.0);
uniform float body_glow_radius : hint_range(0.0, 10.0) = 2.0;
uniform float body_glow_intensity : hint_range(0.0, 10.0) = 1.0;

void fragment() {
    vec4 mask_color = texture(TEXTURE, UV);
    vec4 final_color = vec4(0.0);

    // 动态呼吸因子
    float time_factor = (sin(TIME * cycle_speed) + 1.0) * 0.5;

    // 动态计算武器颜色和亮度
    vec3 current_glow_color = mix(glow_color_1, glow_color_2, time_factor);
    float current_weapon_intensity = glow_intensity * (0.8 + 0.4 * time_factor);

    // ==========================================
    // 第一步：处理本体和武器本身的颜色
    // ==========================================
    if (mask_color.a > 0.05) {
        vec2 lut_uv = vec2(mask_color.r, mask_color.g);
        vec4 real_color = texture(palette_lut, lut_uv);

        // 识别武器
        if (real_color.b > 0.85 && real_color.r < 0.15 && real_color.g < 0.15) {
            final_color = vec4(current_glow_color, mask_color.a);
        } else {
            final_color = vec4(real_color.rgb, mask_color.a);
        }
    }

    // ==========================================
    // 第二步：空气光晕计算 (防显卡罢工版)
    // ==========================================
    float max_r = max(glow_radius, body_glow_radius);

    if (mask_color.a < 0.05 && max_r > 0.0) {
        float w_glow = 0.0;
        float b_glow = 0.0;
        vec2 ps = TEXTURE_PIXEL_SIZE;

        // 🚨 极客硬核修复：强制固定最大循环次数 10，防止 D3D12 编译器罢工跳出！
        const float MAX_SEARCH = 10.0;

        for (float x = -MAX_SEARCH; x <= MAX_SEARCH; x += 1.0) {
            if (x > max_r || x < -max_r) continue; // 软件层面截断

            for (float y = -MAX_SEARCH; y <= MAX_SEARCH; y += 1.0) {
                if (y > max_r || y < -max_r) continue; // 软件层面截断

                vec2 offset = vec2(x, y);
                float dist = length(offset);

                if (dist <= max_r && dist > 0.0) {
                    vec4 neighbor_mask = texture(TEXTURE, UV + offset * ps);
                    if (neighbor_mask.a > 0.05) {
                        vec2 neighbor_uv = vec2(neighbor_mask.r, neighbor_mask.g);
                        vec4 neighbor_real = texture(palette_lut, neighbor_uv);

                        // 发现武器像素
                        if (neighbor_real.b > 0.85 && neighbor_real.r < 0.15 && neighbor_real.g < 0.15) {
                            if (dist <= glow_radius) {
                                w_glow += 1.0 - (dist / glow_radius);
                            }
                        } else {
                            // 发现本体像素
                            if (dist <= body_glow_radius) {
                                b_glow += 1.0 - (dist / body_glow_radius);
                            }
                        }
                    }
                }
            }
        }

        // 🚨 修复数学灾难：基础倍率从 0.02 改回 0.15，保证小半径下亮度充足！
        float w_alpha = clamp(w_glow * 0.15 * current_weapon_intensity, 0.0, 1.0);
        float b_alpha = clamp(b_glow * 0.15 * body_glow_intensity, 0.0, 1.0);

        if (w_alpha > 0.0 || b_alpha > 0.0) {
            float total_alpha = max(w_alpha, b_alpha);
            float weight = w_alpha / (w_alpha + b_alpha + 0.0001);
            vec3 combined_color = mix(body_glow_color, current_glow_color, weight);

            final_color = vec4(combined_color, total_alpha);
        }
    }

    if (final_color.a < 0.01) {
        discard;
    }

    COLOR = final_color;
}