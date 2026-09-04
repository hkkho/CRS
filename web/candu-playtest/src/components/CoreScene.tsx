import { useEffect, useRef } from "react";
import type { PointerEvent } from "react";
import * as THREE from "three";
import type { CanduChannelSnapshot } from "../protocol";
import { getFlowArrow, getFlowDirectionLabel, getHeatColor } from "../visuals";

const CANDU6_ROW_LABELS = ["A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W"] as const;
const CORE_GRID_SIZE = 22;
const CORE_GRID_SPACING = 0.83;
const CORE_GRID_OFFSET = 10.5;
const CORE_GRID_HALF_EXTENT = 9.2;
const CORE_VIEW_HALF_EXTENT = 10.25;

interface CoreSceneProps {
  channels: readonly CanduChannelSnapshot[];
  selectedChannelIndex: number;
  viewMode: "engine2d" | "grid";
  onSelectChannel: (channelIndex: number) => void;
  onChangeViewMode: (viewMode: "engine2d" | "grid") => void;
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
  const cameraRef = useRef<THREE.OrthographicCamera | null>(null);
  const meshRef = useRef<THREE.InstancedMesh | null>(null);
  const flowMeshRef = useRef<THREE.InstancedMesh | null>(null);
  const markerRef = useRef<THREE.Mesh | null>(null);
  const channelsRef = useRef(channels);
  const selectRef = useRef(onSelectChannel);
  const changeViewRef = useRef(onChangeViewMode);
  const raycasterRef = useRef(new THREE.Raycaster());
  const pointerRef = useRef(new THREE.Vector2());

  useEffect(() => {
    channelsRef.current = channels;
  }, [channels]);

  useEffect(() => {
    selectRef.current = onSelectChannel;
  }, [onSelectChannel]);

  useEffect(() => {
    changeViewRef.current = onChangeViewMode;
  }, [onChangeViewMode]);

  useEffect(() => {
    if (viewMode !== "engine2d" || mountRef.current === null) {
      return;
    }

    const mount = mountRef.current;
    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true, powerPreference: "high-performance" });
    } catch {
      changeViewRef.current("grid");
      return;
    }

    rendererRef.current = renderer;
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    renderer.setClearColor(0x07131f, 0);
    renderer.domElement.className = "core-canvas";
    renderer.domElement.setAttribute("role", "img");
    renderer.domElement.setAttribute(
      "aria-label",
      "Interactive engine-rendered 2D CANDU 6 channel heat map. Use Grid Map for keyboard channel selection.",
    );
    mount.appendChild(renderer.domElement);

    const scene = new THREE.Scene();
    const camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 100);
    camera.position.set(0, 0, 20);
    camera.lookAt(0, 0, 0);
    cameraRef.current = camera;

    const ambient = new THREE.AmbientLight(0x9adbd2, 1.85);
    const key = new THREE.DirectionalLight(0xffdf9a, 2.8);
    key.position.set(-4, 7, 10);
    scene.add(ambient, key);

    const group = new THREE.Group();
    scene.add(group);

    const floor = new THREE.Mesh(
      new THREE.PlaneGeometry(CORE_VIEW_HALF_EXTENT * 2, CORE_VIEW_HALF_EXTENT * 2),
      new THREE.MeshBasicMaterial({ color: 0x0b1c2b, transparent: true, opacity: 0.84 }),
    );
    floor.position.z = -0.28;
    group.add(floor);

    const grid = createCoreGrid();
    group.add(grid);

    const outline = new THREE.LineLoop(
      new THREE.BufferGeometry().setFromPoints([
        new THREE.Vector3(-CORE_GRID_HALF_EXTENT, -CORE_GRID_HALF_EXTENT, 0),
        new THREE.Vector3(CORE_GRID_HALF_EXTENT, -CORE_GRID_HALF_EXTENT, 0),
        new THREE.Vector3(CORE_GRID_HALF_EXTENT, CORE_GRID_HALF_EXTENT, 0),
        new THREE.Vector3(-CORE_GRID_HALF_EXTENT, CORE_GRID_HALF_EXTENT, 0),
      ]),
      new THREE.LineBasicMaterial({ color: 0x5ed7c5, transparent: true, opacity: 0.7 }),
    );
    outline.position.z = -0.02;
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
    mesh.frustumCulled = false;
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

    const flowGeometry = new THREE.ConeGeometry(0.075, 0.18, 3);
    const flowMaterial = new THREE.MeshBasicMaterial({ color: 0xd7fff1, transparent: true, opacity: 0.72 });
    const flowMesh = new THREE.InstancedMesh(flowGeometry, flowMaterial, initialChannels.length);
    flowMesh.frustumCulled = false;
    flowMesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    initialChannels.forEach((channel, index) => {
      setFlowArrowTransform(dummy, channel);
      flowMesh.setMatrixAt(index, dummy.matrix);
    });
    flowMesh.instanceMatrix.needsUpdate = true;
    flowMeshRef.current = flowMesh;
    group.add(flowMesh);

    const marker = new THREE.Mesh(
      new THREE.RingGeometry(0.37, 0.43, 32),
      new THREE.MeshBasicMaterial({ color: 0xf9e4ac, transparent: true, opacity: 0.98 }),
    );
    markerRef.current = marker;
    group.add(marker);

    const resize = () => {
      const width = Math.max(1, mount.clientWidth);
      const height = Math.max(1, mount.clientHeight);
      const aspect = width / height;
      const halfWidth = Math.max(CORE_VIEW_HALF_EXTENT, CORE_VIEW_HALF_EXTENT * aspect);
      const halfHeight = Math.max(CORE_VIEW_HALF_EXTENT, CORE_VIEW_HALF_EXTENT / aspect);
      renderer.setSize(width, height, false);
      camera.left = -halfWidth;
      camera.right = halfWidth;
      camera.top = halfHeight;
      camera.bottom = -halfHeight;
      camera.updateProjectionMatrix();
    };
    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(mount);

    let animationFrame = 0;
    const reducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
    const render = (time: number) => {
      animationFrame = window.requestAnimationFrame(render);
      if (!reducedMotion) {
        const pulse = 1 + Math.sin(time * 0.004) * 0.05;
        marker.scale.setScalar(pulse);
      }
      renderer.render(scene, camera);
    };
    animationFrame = window.requestAnimationFrame(render);

    return () => {
      window.cancelAnimationFrame(animationFrame);
      observer.disconnect();
      renderer.dispose();
      geometry.dispose();
      material.dispose();
      flowGeometry.dispose();
      flowMaterial.dispose();
      floor.geometry.dispose();
      (floor.material as THREE.Material).dispose();
      grid.geometry.dispose();
      (grid.material as THREE.Material).dispose();
      outline.geometry.dispose();
      (outline.material as THREE.Material).dispose();
      marker.geometry.dispose();
      (marker.material as THREE.Material).dispose();
      renderer.domElement.remove();
      rendererRef.current = null;
      cameraRef.current = null;
      meshRef.current = null;
      flowMeshRef.current = null;
      markerRef.current = null;
    };
  }, [viewMode]);

  useEffect(() => {
    const mesh = meshRef.current;
    const flowMesh = flowMeshRef.current;
    const marker = markerRef.current;
    if (mesh === null || flowMesh === null || marker === null) {
      return;
    }

    const dummy = new THREE.Object3D();
    channels.forEach((channel, index) => {
      setInstanceTransform(dummy, channel);
      mesh.setMatrixAt(index, dummy.matrix);
      mesh.setColorAt(index, new THREE.Color(getHeatColor(channel.localPowerFraction)));
      setFlowArrowTransform(dummy, channel);
      flowMesh.setMatrixAt(index, dummy.matrix);
    });
    mesh.instanceMatrix.needsUpdate = true;
    flowMesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor !== null) {
      mesh.instanceColor.needsUpdate = true;
    }
    const selected = channels[selectedChannelIndex];
    if (selected !== undefined) {
      const markerPosition = getChannelPosition(selected);
      marker.position.set(markerPosition.x, markerPosition.y, 0.34);
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
          <p className="scene-caption">CANDU 6 · engine-rendered 2D · 380 channels · alternating coolant / fuelling flow</p>
        </div>
        <div className="view-toggle" role="group" aria-label="Core rendering mode">
          <button
            className={viewMode === "engine2d" ? "view-toggle-button is-active" : "view-toggle-button"}
            type="button"
            aria-pressed={viewMode === "engine2d"}
            aria-label="Use engine-rendered 2D view"
            title="GPU-rendered 2D core view"
            onClick={() => onChangeViewMode("engine2d")}
          >
            ENGINE 2D
          </button>
          <button
            className={viewMode === "grid" ? "view-toggle-button is-active" : "view-toggle-button"}
            type="button"
            aria-pressed={viewMode === "grid"}
            aria-label="Use accessible grid map"
            title="Keyboard-accessible HTML grid map"
            onClick={() => onChangeViewMode("grid")}
          >
            GRID MAP
          </button>
        </div>
      </div>
      {viewMode === "engine2d" ? (
        <div
          ref={mountRef}
          className="core-canvas-mount"
          onPointerDown={handlePointerDown}
          title="Select a reactor channel in the engine-rendered 2D view"
        />
      ) : (
        <CoreMapGrid channels={channels} selectedChannelIndex={selectedChannelIndex} onSelectChannel={onSelectChannel} />
      )}
    </div>
  );
}

function CoreMapGrid({
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

function createCoreGrid(): THREE.LineSegments {
  const positions: number[] = [];
  const edge = CORE_GRID_HALF_EXTENT - 0.07;
  for (let index = 0; index <= CORE_GRID_SIZE; index += 1) {
    const coordinate = -edge + index * ((edge * 2) / CORE_GRID_SIZE);
    positions.push(coordinate, -edge, -0.12, coordinate, edge, -0.12);
    positions.push(-edge, coordinate, -0.12, edge, coordinate, -0.12);
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute("position", new THREE.Float32BufferAttribute(positions, 3));
  return new THREE.LineSegments(
    geometry,
    new THREE.LineBasicMaterial({ color: 0x31556a, transparent: true, opacity: 0.3 }),
  );
}

function getChannelPosition(channel: CanduChannelSnapshot): THREE.Vector3 {
  return new THREE.Vector3(
    (channel.gridColumn - CORE_GRID_OFFSET) * CORE_GRID_SPACING,
    (CORE_GRID_OFFSET - channel.gridRow) * CORE_GRID_SPACING,
    0,
  );
}

function setInstanceTransform(dummy: THREE.Object3D, channel: CanduChannelSnapshot): void {
  const position = getChannelPosition(channel);
  dummy.position.set(position.x, position.y, 0.1);
  dummy.rotation.set(Math.PI / 2, 0, 0);
  dummy.scale.set(1, 0.88 + channel.localPowerFraction * 0.24, 1);
  dummy.updateMatrix();
}

function setFlowArrowTransform(dummy: THREE.Object3D, channel: CanduChannelSnapshot): void {
  const position = getChannelPosition(channel);
  const pointsTowardEndB = channel.flowDirection === "toward-end-b";
  dummy.position.set(position.x + (pointsTowardEndB ? 0.14 : -0.14), position.y - 0.16, 0.27);
  dummy.rotation.set(0, 0, pointsTowardEndB ? -Math.PI / 2 : Math.PI / 2);
  dummy.scale.setScalar(0.78);
  dummy.updateMatrix();
}
