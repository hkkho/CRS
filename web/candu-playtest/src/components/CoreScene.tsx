import { useEffect, useRef } from "react";
import type { PointerEvent } from "react";
import * as THREE from "three";
import type { CanduChannelSnapshot } from "../protocol";
import { getFlowArrow, getFlowDirectionLabel, getHeatColor } from "../visuals";

const CANDU6_ROW_LABELS = ["A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W"] as const;

interface CoreSceneProps {
  channels: readonly CanduChannelSnapshot[];
  selectedChannelIndex: number;
  viewMode: "3d" | "2d";
  onSelectChannel: (channelIndex: number) => void;
  onChangeViewMode: (viewMode: "3d" | "2d") => void;
}

export function CoreScene({
  channels,
  selectedChannelIndex,
  viewMode,
  onSelectChannel,
  onChangeViewMode,
}: CoreSceneProps) {
  const mountRef = useRef<HTMLDivElement>(null);
  const rendererRef = useRef<THREE.WebGLRenderer | null>(null);
  const cameraRef = useRef<THREE.PerspectiveCamera | null>(null);
  const meshRef = useRef<THREE.InstancedMesh | null>(null);
  const markerRef = useRef<THREE.Mesh | null>(null);
  const groupRef = useRef<THREE.Group | null>(null);
  const channelsRef = useRef(channels);
  const selectRef = useRef(onSelectChannel);
  const raycasterRef = useRef(new THREE.Raycaster());
  const pointerRef = useRef(new THREE.Vector2());

  useEffect(() => {
    channelsRef.current = channels;
  }, [channels]);

  useEffect(() => {
    selectRef.current = onSelectChannel;
  }, [onSelectChannel]);

  useEffect(() => {
    if (viewMode !== "3d" || mountRef.current === null) {
      return;
    }

    const mount = mountRef.current;
    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true, powerPreference: "high-performance" });
    } catch {
      onChangeViewMode("2d");
      return;
    }

    rendererRef.current = renderer;
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setClearColor(0x07131f, 0);
    renderer.domElement.className = "core-canvas";
    renderer.domElement.setAttribute("role", "img");
    renderer.domElement.setAttribute("aria-label", "Interactive 3D reactor channel heat map. Use the 2D map for keyboard channel selection.");
    mount.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 100);
    camera.position.set(0, 11.8, 17.4);
    camera.lookAt(0, 0, 0);
    cameraRef.current = camera;

    const ambient = new THREE.HemisphereLight(0x9adbd2, 0x07131f, 2.1);
    const key = new THREE.DirectionalLight(0xffdf9a, 3.4);
    key.position.set(3, 10, 7);
    scene.add(ambient, key);

    const group = new THREE.Group();
    group.rotation.x = -0.1;
    groupRef.current = group;
    scene.add(group);

    const floor = new THREE.Mesh(
      new THREE.CircleGeometry(10.1, 64),
      new THREE.MeshBasicMaterial({ color: 0x0b1c2b, transparent: true, opacity: 0.78 }),
    );
    floor.rotation.x = -Math.PI / 2;
    floor.position.y = -0.19;
    group.add(floor);

    const outline = new THREE.Mesh(
      new THREE.RingGeometry(9.5, 9.57, 64),
      new THREE.MeshBasicMaterial({ color: 0x274459, transparent: true, opacity: 0.92, side: THREE.DoubleSide }),
    );
    outline.rotation.x = -Math.PI / 2;
    outline.position.y = -0.16;
    group.add(outline);

    const geometry = new THREE.CylinderGeometry(0.23, 0.31, 0.16, 6);
    const material = new THREE.MeshStandardMaterial({
      roughness: 0.38,
      metalness: 0.2,
      vertexColors: true,
      emissive: 0x07131f,
      emissiveIntensity: 0.44,
    });
    const initialChannels = channelsRef.current;
    const mesh = new THREE.InstancedMesh(geometry, material, initialChannels.length);
    mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    const dummy = new THREE.Object3D();
    initialChannels.forEach((channel, index) => {
      setInstanceTransform(dummy, channel);
      mesh.setMatrixAt(index, dummy.matrix);
      mesh.setColorAt(index, new THREE.Color(getHeatColor(channel.localPowerFraction)));
    });
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor !== null) {
      mesh.instanceColor.needsUpdate = true;
    }
    meshRef.current = mesh;
    group.add(mesh);

    const marker = new THREE.Mesh(
      new THREE.TorusGeometry(0.43, 0.035, 8, 32),
      new THREE.MeshBasicMaterial({ color: 0xf9e4ac, transparent: true, opacity: 0.98 }),
    );
    marker.rotation.x = -Math.PI / 2;
    markerRef.current = marker;
    group.add(marker);

    const resize = () => {
      const width = Math.max(1, mount.clientWidth);
      const height = Math.max(1, mount.clientHeight);
      renderer.setSize(width, height, false);
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
    };
    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(mount);

    let animationFrame = 0;
    const reducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
    const render = () => {
      animationFrame = window.requestAnimationFrame(render);
      if (!reducedMotion) {
        group.rotation.y += 0.0016;
      }
      renderer.render(scene, camera);
    };
    render();

    return () => {
      window.cancelAnimationFrame(animationFrame);
      observer.disconnect();
      renderer.dispose();
      geometry.dispose();
      material.dispose();
      floor.geometry.dispose();
      (floor.material as THREE.Material).dispose();
      outline.geometry.dispose();
      (outline.material as THREE.Material).dispose();
      marker.geometry.dispose();
      (marker.material as THREE.Material).dispose();
      renderer.domElement.remove();
      rendererRef.current = null;
      cameraRef.current = null;
      meshRef.current = null;
      markerRef.current = null;
      groupRef.current = null;
    };
  }, [onChangeViewMode, viewMode]);

  useEffect(() => {
    const mesh = meshRef.current;
    const marker = markerRef.current;
    if (mesh === null || marker === null) {
      return;
    }

    const dummy = new THREE.Object3D();
    channels.forEach((channel, index) => {
      setInstanceTransform(dummy, channel);
      mesh.setMatrixAt(index, dummy.matrix);
      mesh.setColorAt(index, new THREE.Color(getHeatColor(channel.localPowerFraction)));
    });
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor !== null) {
      mesh.instanceColor.needsUpdate = true;
    }
    const selected = channels[selectedChannelIndex];
    if (selected !== undefined) {
      const markerPosition = getChannelPosition(selected);
      marker.position.set(markerPosition.x, 0.12, markerPosition.z);
    }
  }, [channels, selectedChannelIndex]);

  const handlePointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const renderer = rendererRef.current;
    const camera = cameraRef.current;
    const mesh = meshRef.current;
    if (renderer === null || camera === null || mesh === null) {
      return;
    }
    const bounds = renderer.domElement.getBoundingClientRect();
    pointerRef.current.set(
      ((event.clientX - bounds.left) / bounds.width) * 2 - 1,
      -((event.clientY - bounds.top) / bounds.height) * 2 + 1,
    );
    raycasterRef.current.setFromCamera(pointerRef.current, camera);
    const hit = raycasterRef.current.intersectObject(mesh, false)[0];
    if (hit !== undefined && hit.instanceId !== undefined) {
      selectRef.current(hit.instanceId);
    }
  };

  return (
    <div className="core-scene-shell">
      <div className="core-scene-toolbar">
        <div>
          <p className="panel-kicker">Core surface</p>
          <p className="scene-caption">CANDU 6 · 380 channels · alternating coolant / fuelling flow</p>
        </div>
        <div className="view-toggle" role="group" aria-label="Core map view mode">
          <button
            className={viewMode === "3d" ? "view-toggle-button is-active" : "view-toggle-button"}
            type="button"
            aria-pressed={viewMode === "3d"}
            onClick={() => onChangeViewMode("3d")}
          >
            3D
          </button>
          <button
            className={viewMode === "2d" ? "view-toggle-button is-active" : "view-toggle-button"}
            type="button"
            aria-pressed={viewMode === "2d"}
            onClick={() => onChangeViewMode("2d")}
          >
            2D
          </button>
        </div>
      </div>
      {viewMode === "3d" ? (
        <div
          ref={mountRef}
          className="core-canvas-mount"
          onPointerDown={handlePointerDown}
          title="Select a reactor channel"
        />
      ) : (
        <CoreMap2d channels={channels} selectedChannelIndex={selectedChannelIndex} onSelectChannel={onSelectChannel} />
      )}
    </div>
  );
}

function CoreMap2d({
  channels,
  selectedChannelIndex,
  onSelectChannel,
}: Pick<CoreSceneProps, "channels" | "selectedChannelIndex" | "onSelectChannel">) {
  return (
    <div className="core-map-2d" role="grid" aria-label="Keyboard-accessible CANDU 6 channel heat map with alternating fuelling directions">
      <div className="core-map-column-labels" aria-hidden="true">
        {Array.from({ length: 22 }, (_, index) => <span key={index}>{String(index + 1).padStart(2, "0")}</span>)}
      </div>
      <div className="core-map-row-labels" aria-hidden="true">
        {CANDU6_ROW_LABELS.map((label) => <span key={label}>{label}</span>)}
      </div>
      {channels.map((channel) => (
        <button
          className={channel.channelIndex === selectedChannelIndex ? "channel-cell is-selected" : "channel-cell"}
          key={channel.channelIndex}
          type="button"
          role="gridcell"
          aria-label={`Channel ${channel.channelIndex}, ${CANDU6_ROW_LABELS[channel.gridRow]}${String(channel.gridColumn + 1).padStart(2, "0")}, power ${(channel.localPowerFraction * 100).toFixed(1)} percent, ${getFlowDirectionLabel(channel.flowDirection)}`}
          aria-pressed={channel.channelIndex === selectedChannelIndex}
          title={`CH ${channel.channelIndex} · ${CANDU6_ROW_LABELS[channel.gridRow]}${String(channel.gridColumn + 1).padStart(2, "0")} · ${(channel.localPowerFraction * 100).toFixed(1)}% power · ${getFlowDirectionLabel(channel.flowDirection)}`}
          style={{
            gridColumn: channel.gridColumn + 1,
            gridRow: channel.gridRow + 1,
            background: getHeatColor(channel.localPowerFraction),
          }}
          onClick={() => onSelectChannel(channel.channelIndex)}
        >
          <span className="channel-cell-index">{channel.channelIndex}</span>
          <span className="channel-cell-flow" aria-hidden="true">{getFlowArrow(channel.flowDirection)}</span>
        </button>
      ))}
    </div>
  );
}

function getChannelPosition(channel: CanduChannelSnapshot): THREE.Vector3 {
  return new THREE.Vector3((channel.gridColumn - 10.5) * 0.83, 0, (channel.gridRow - 10.5) * 0.83);
}

function setInstanceTransform(dummy: THREE.Object3D, channel: CanduChannelSnapshot): void {
  const position = getChannelPosition(channel);
  dummy.position.set(position.x, 0.1 + Math.sin(channel.channelIndex * 0.14) * 0.08, position.z);
  dummy.rotation.set(Math.PI / 2, 0, 0);
  dummy.scale.set(1, 1, 0.85 + channel.localPowerFraction * 0.22);
  dummy.updateMatrix();
}
