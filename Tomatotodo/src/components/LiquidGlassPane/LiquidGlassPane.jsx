import { useEffect, useId, useRef, useState } from "react";
import "./LiquidGlassPane.css";

const surfaceHeight = (x) => Math.pow(1 - Math.pow(1 - x, 4), 0.25);
const filterCache = new Map();

function calculateRefractionProfile(thickness, bezel, ior, samples = 128) {
  const eta = 1 / ior;
  const profile = new Float64Array(samples);

  for (let index = 0; index < samples; index += 1) {
    const x = index / samples;
    const y = surfaceHeight(x);
    const dx = x < 1 ? 0.0001 : -0.0001;
    const derivative = (surfaceHeight(x + dx) - y) / dx;
    const magnitude = Math.sqrt(derivative * derivative + 1);
    const nx = -derivative / magnitude;
    const ny = -1 / magnitude;
    const dot = ny;
    const k = 1 - eta * eta * (1 - dot * dot);
    if (k < 0) continue;
    const root = Math.sqrt(k);
    const refractedX = -(eta * dot + root) * nx;
    const refractedY = eta - (eta * dot + root) * ny;
    profile[index] = refractedX * ((y * bezel + thickness) / refractedY);
  }

  return profile;
}

function generateDisplacementMap(width, height, radius, bezel, profile, maxDisplacement) {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext("2d");
  const image = context.createImageData(width, height);
  const pixels = image.data;

  for (let index = 0; index < pixels.length; index += 4) {
    pixels[index] = 128;
    pixels[index + 1] = 128;
    pixels[index + 3] = 255;
  }

  const radiusSquared = radius * radius;
  const outerSquared = (radius + 1) ** 2;
  const innerSquared = Math.max(radius - bezel, 0) ** 2;
  const middleWidth = width - radius * 2;
  const middleHeight = height - radius * 2;

  for (let yPosition = 0; yPosition < height; yPosition += 1) {
    for (let xPosition = 0; xPosition < width; xPosition += 1) {
      const x = xPosition < radius
        ? xPosition - radius
        : xPosition >= width - radius ? xPosition - radius - middleWidth : 0;
      const y = yPosition < radius
        ? yPosition - radius
        : yPosition >= height - radius ? yPosition - radius - middleHeight : 0;
      const distanceSquared = x * x + y * y;
      if (distanceSquared > outerSquared || distanceSquared < innerSquared) continue;

      const distance = Math.sqrt(distanceSquared);
      if (!distance) continue;
      const fromEdge = radius - distance;
      const opacity = distanceSquared < radiusSquared
        ? 1
        : 1 - (distance - radius) / (Math.sqrt(outerSquared) - radius);
      if (opacity <= 0) continue;

      const profileIndex = Math.min(
        Math.max(0, ((fromEdge / bezel) * profile.length) | 0),
        profile.length - 1,
      );
      const displacement = profile[profileIndex] || 0;
      const normalizedX = (-(x / distance) * displacement) / maxDisplacement;
      const normalizedY = (-(y / distance) * displacement) / maxDisplacement;
      const pixelIndex = (yPosition * width + xPosition) * 4;
      pixels[pixelIndex] = Math.max(0, Math.min(255, 128 + normalizedX * 127 * opacity + 0.5));
      pixels[pixelIndex + 1] = Math.max(0, Math.min(255, 128 + normalizedY * 127 * opacity + 0.5));
    }
  }

  context.putImageData(image, 0, 0);
  return canvas.toDataURL();
}

function generateSpecularMap(width, height, radius, bezel, angle = Math.PI / 3) {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  const context = canvas.getContext("2d");
  const image = context.createImageData(width, height);
  const pixels = image.data;
  const radiusSquared = radius * radius;
  const outerSquared = (radius + 1) ** 2;
  const innerSquared = Math.max(radius - bezel, 0) ** 2;
  const middleWidth = width - radius * 2;
  const middleHeight = height - radius * 2;
  const lightX = Math.cos(angle);
  const lightY = Math.sin(angle);

  for (let yPosition = 0; yPosition < height; yPosition += 1) {
    for (let xPosition = 0; xPosition < width; xPosition += 1) {
      const x = xPosition < radius
        ? xPosition - radius
        : xPosition >= width - radius ? xPosition - radius - middleWidth : 0;
      const y = yPosition < radius
        ? yPosition - radius
        : yPosition >= height - radius ? yPosition - radius - middleHeight : 0;
      const distanceSquared = x * x + y * y;
      if (distanceSquared > outerSquared || distanceSquared < innerSquared) continue;

      const distance = Math.sqrt(distanceSquared);
      if (!distance) continue;
      const fromEdge = radius - distance;
      const opacity = distanceSquared < radiusSquared
        ? 1
        : 1 - (distance - radius) / (Math.sqrt(outerSquared) - radius);
      if (opacity <= 0) continue;

      const dot = Math.abs((x / distance) * lightX + (-y / distance) * lightY);
      const edge = Math.sqrt(Math.max(0, 1 - (1 - fromEdge) ** 2));
      const coefficient = dot * edge;
      const color = Math.max(0, Math.min(255, (255 * coefficient) | 0));
      const alpha = Math.max(0, Math.min(255, (color * coefficient * opacity) | 0));
      const pixelIndex = (yPosition * width + xPosition) * 4;
      pixels[pixelIndex] = color;
      pixels[pixelIndex + 1] = color;
      pixels[pixelIndex + 2] = color;
      pixels[pixelIndex + 3] = alpha;
    }
  }

  context.putImageData(image, 0, 0);
  return canvas.toDataURL();
}

export default function LiquidGlassPane({ children, borderRadius = 30, className = "" }) {
  const paneRef = useRef(null);
  const blurReplicaRef = useRef(null);
  const replicaRef = useRef(null);
  const filterId = `liquid-glass-${useId().replace(/:/g, "-")}`;
  const [filter, setFilter] = useState(null);

  useEffect(() => {
    const pane = paneRef.current;
    if (!pane) return undefined;
    let frame = 0;

    const rebuild = () => {
      window.cancelAnimationFrame(frame);
      frame = window.requestAnimationFrame(() => {
        const rect = pane.getBoundingClientRect();
        const width = Math.max(2, Math.round(rect.width));
        const height = Math.max(2, Math.round(rect.height));
        const radius = Math.min(borderRadius, width / 2 - 1, height / 2 - 1);
        const bezel = Math.max(8, Math.min(26, radius - 1));
        const cacheKey = `${width}x${height}-${radius}-${bezel}`;
        const cached = filterCache.get(cacheKey);
        if (cached) {
          setFilter(cached);
          return;
        }
        // React Bits FluidGlass uses a low IOR (1.15) and a thick, clear medium.
        // Keep the same optical character while using a DOM-safe displacement map.
        const profile = calculateRefractionProfile(10, bezel, 1.15);
        const maxDisplacement = Math.max(...Array.from(profile).map(Math.abs)) || 1;
        const nextFilter = {
          width,
          height,
          // Keep the flat centre optically calm, but make the rounded edge
          // visibly pull and bend the page beneath it.
          scale: Math.max(74, Math.min(104, bezel * 3.7)),
          displacement: generateDisplacementMap(width, height, radius, bezel, profile, maxDisplacement),
          specular: generateSpecularMap(width, height, radius, bezel * 2.35),
        };
        if (filterCache.size >= 8) filterCache.delete(filterCache.keys().next().value);
        filterCache.set(cacheKey, nextFilter);
        setFilter(nextFilter);
      });
    };

    rebuild();
    const observer = new ResizeObserver(rebuild);
    observer.observe(pane);
    return () => {
      observer.disconnect();
      window.cancelAnimationFrame(frame);
    };
  }, [borderRadius]);

  useEffect(() => {
    const pane = paneRef.current;
    const blurReplica = blurReplicaRef.current;
    const replica = replicaRef.current;
    const source = document.querySelector(".app-shell");
    if (!pane || !blurReplica || !replica || !source) return undefined;

    const prepareClone = () => {
      const nextClone = source.cloneNode(true);
      nextClone.setAttribute("aria-hidden", "true");
      nextClone.querySelectorAll(".quick-note-shell, .quick-note-launcher, .quick-note-history").forEach((node) => node.remove());
      nextClone.querySelectorAll("[id]").forEach((node) => node.removeAttribute("id"));
      nextClone.querySelectorAll("button, input, textarea, select, a").forEach((node) => node.setAttribute("tabindex", "-1"));
      return nextClone;
    };
    const blurClone = prepareClone();
    const edgeClone = prepareClone();
    blurReplica.replaceChildren(blurClone);
    replica.replaceChildren(edgeClone);

    let frame = 0;
    const alignReplica = () => {
      const rect = pane.getBoundingClientRect();
      const blurPadding = 72;
      blurClone.style.position = "absolute";
      blurClone.style.left = `${-rect.left + blurPadding}px`;
      blurClone.style.top = `${-rect.top + blurPadding}px`;
      blurClone.style.width = `${window.innerWidth}px`;
      blurClone.style.height = `${window.innerHeight}px`;

      edgeClone.style.position = "absolute";
      edgeClone.style.left = `${-rect.left}px`;
      edgeClone.style.top = `${-rect.top}px`;
      edgeClone.style.width = `${window.innerWidth}px`;
      edgeClone.style.height = `${window.innerHeight}px`;
      edgeClone.style.transformOrigin = `${rect.left + rect.width / 2}px ${rect.top + rect.height / 2}px`;
      edgeClone.style.transform = "scale(1.090)";
      frame = window.requestAnimationFrame(alignReplica);
    };
    alignReplica();

    return () => {
      window.cancelAnimationFrame(frame);
      blurReplica.replaceChildren();
      replica.replaceChildren();
    };
  }, []);

  return (
    <div
      className={`liquid-glass-pane ${className}`}
      ref={paneRef}
      style={{ borderRadius: `${borderRadius}px` }}
    >
      <svg className="liquid-glass-pane__filter" aria-hidden="true">
        <defs>
          {filter ? (
            <filter id={filterId} x="-18%" y="-18%" width="136%" height="136%" colorInterpolationFilters="sRGB">
              <feGaussianBlur in="SourceGraphic" stdDeviation="24" result="blurredSource" />
              <feImage href={filter.displacement} x="0" y="0" width={filter.width} height={filter.height} result="displacementMap" />
              <feDisplacementMap
                in="blurredSource"
                in2="displacementMap"
                scale={filter.scale}
                xChannelSelector="R"
                yChannelSelector="G"
                result="displaced"
              />
              <feTurbulence
                type="fractalNoise"
                baseFrequency="0.009 0.024"
                numOctaves="1"
                seed="11"
                result="liquidRipple"
              />
              <feDisplacementMap
                in="displaced"
                in2="liquidRipple"
                scale="26"
                xChannelSelector="R"
                yChannelSelector="G"
                result="rippledGlass"
              />
              <feColorMatrix in="rippledGlass" type="saturate" values="3.4" result="saturated" />
              <feImage href={filter.specular} x="0" y="0" width={filter.width} height={filter.height} result="specularLayer" />
              <feComposite in="saturated" in2="specularLayer" operator="in" result="specularMask" />
              <feComponentTransfer in="specularLayer" result="fadedSpecular">
                <feFuncA type="linear" slope="0.58" />
              </feComponentTransfer>
              <feBlend in="specularMask" in2="displaced" mode="normal" result="saturatedGlass" />
              <feBlend in="fadedSpecular" in2="saturatedGlass" mode="normal" />
            </filter>
          ) : null}
        </defs>
      </svg>
      <div className="liquid-glass-pane__blur-replica" ref={blurReplicaRef} aria-hidden="true" />
      <div
        className="liquid-glass-pane__refraction"
        aria-hidden="true"
      >
        <div className="liquid-glass-pane__replica" ref={replicaRef} />
      </div>
      <div className="liquid-glass-pane__cover" aria-hidden="true" />
      <div className="liquid-glass-pane__tint" aria-hidden="true" />
      <div className="liquid-glass-pane__dispersion" aria-hidden="true" />
      <div className="liquid-glass-pane__reflect" aria-hidden="true" />
      <div className="liquid-glass-pane__sharp" aria-hidden="true" />
      <div className="liquid-glass-pane__content">{children}</div>
    </div>
  );
}
