shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;
uniform bool hazy=false;
varying vec3 sphere_coord;
float hash3(vec3 p){p=fract(p*.1031);p+=dot(p,p.yzx+33.33);return fract((p.x+p.y)*p.z);}
float noise3(vec3 p){vec3 i=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);return mix(mix(mix(hash3(i),hash3(i+vec3(1,0,0)),f.x),mix(hash3(i+vec3(0,1,0)),hash3(i+vec3(1,1,0)),f.x),f.y),mix(mix(hash3(i+vec3(0,0,1)),hash3(i+vec3(1,0,1)),f.x),mix(hash3(i+vec3(0,1,1)),hash3(i+vec3(1,1,1)),f.x),f.y),f.z);}
float fbm(vec3 p){return noise3(p)*.57+noise3(p*2.03)*.26+noise3(p*4.07)*.12+noise3(p*8.17)*.05;}
void vertex(){sphere_coord=NORMAL;}
void fragment(){
 vec3 p=normalize(sphere_coord); float n=fbm(p*7.0);float fine=fbm(p*160.0);
 if(hazy){float bands=fbm(p*4.0+vec3(n*2.0,p.y*12.0,0)); ALBEDO=mix(vec3(.36,.20,.08),vec3(.82,.60,.29),.50+bands*.46);ALBEDO*=.95+fine*.08;ROUGHNESS=.94;SPECULAR=.16;}
 else{float warp=fbm(p*11.0)*1.6;float veins=abs(sin((p.x*.7+p.z*.38+p.y*.31)*62.0+warp*7.0));float veins2=abs(sin(dot(p,vec3(-.42,.61,.58))*83.0+warp*5.0));float crack=1.0-smoothstep(.025,.080,min(veins,veins2));float stain=1.0-smoothstep(.06,.31,min(veins,veins2));ALBEDO=mix(vec3(.85,.84,.73),vec3(.61,.51,.40),stain*.44);ALBEDO=mix(ALBEDO,vec3(.23,.15,.10),crack*.74);ALBEDO*=.87+n*.15+fine*.08;ROUGHNESS=.76;SPECULAR=.24;}
}
