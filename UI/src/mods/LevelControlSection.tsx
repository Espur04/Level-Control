import React from "react";
import { bindLocalValue, trigger, useValue } from "cs2/api";
import { SelectedInfoSectionBase, selectedInfo } from "cs2/bindings";
import { FOCUS_AUTO, FocusDisabled } from "cs2/input";
import * as l10n from "cs2/l10n";
import { getModule } from "cs2/modding";
import { Button, Panel, PanelSection, PanelSectionRow, Portal } from "cs2/ui";
import mod from "mod.json";

interface LevelControlSectionProps extends SelectedInfoSectionBase {
  currentLevel: number;
  targetLevel: number;
  availableLevels: number[];
  locked: boolean;
  buildingEntityIndex: number;
  buildingEntityVersion: number;
}

const panelVisible = bindLocalValue(false);
const infoLayoutTheme = getModule("game-ui/game/components/game-main-screen.module.scss", "classes") as {
  infoLayout: string;
};
const selectedInfoTheme = getModule("game-ui/game/components/selected-info-panel/selected-info-panel.module.scss", "classes") as {
  selectedInfoPanel: string;
};
const nativePanelTheme = getModule("game-ui/common/panel/panel.module.scss", "classes") as {
  panel: string;
};

const ToolButton = getModule("game-ui/game/components/tool-options/tool-button/tool-button.tsx", "ToolButton") as (props: {
  id?: string;
  focusKey?: any;
  src: string;
  selected?: boolean;
  multiSelect?: boolean;
  disabled?: boolean;
  tooltip?: string;
  className?: string;
  onSelect?: () => void;
  style?: React.CSSProperties;
}) => JSX.Element;

const toolButtonTheme = getModule("game-ui/game/components/tool-options/tool-button/tool-button.module.scss", "classes") as {
  button: string;
};

const controlIcons = {
  panel: "coui://uil/Standard/HouseMainElements.svg",
  locked: "coui://uil/Standard/LockClosed.svg",
  unlocked: "coui://uil/Standard/LockOpen.svg",
};

const levels = [1, 2, 3, 4, 5];
const panelFallbackPosition = { x: 0.4, y: 0.08 };

export const LevelControlSection = (componentList: Record<string, unknown>) => {
  componentList["Level_Control.LevelControlSelectionSection"] = (props: LevelControlSectionProps) => (
    <LevelControlPanel {...props} />
  );

  return componentList;
};

const LevelControlPanel = (props: LevelControlSectionProps) => {
  const { translate } = l10n.useLocalization();
  const isOpen = useValue(panelVisible);
  const selectedEntity = useValue(selectedInfo.selectedEntity$);
  const isCurrentSelection = selectedEntity.index === props.buildingEntityIndex
    && selectedEntity.version === props.buildingEntityVersion;
  const selectionKey = `${props.buildingEntityIndex}:${props.buildingEntityVersion}`;
  const [measuredPosition, setMeasuredPosition] = React.useState({
    selectionKey: "",
    ...panelFallbackPosition,
  });
  const [panelDebug, setPanelDebug] = React.useState("Waiting for panel layout");
  const positionReady = measuredPosition.selectionKey === selectionKey;

  const findSelectedPanel = () => Array.from(
    document.querySelectorAll<HTMLElement>(`.${nativePanelTheme.panel}.${selectedInfoTheme.selectedInfoPanel}`),
  ).find(panel => panel.id !== "level-control-secondary" && panel.offsetWidth > 0) ?? null;

  const getAlignedPosition = () => {
    const infoLayout = document.querySelector(`.${infoLayoutTheme.infoLayout}`) as HTMLElement | null;
    const selectedPanel = findSelectedPanel();
    if (!infoLayout || !selectedPanel || window.innerWidth === 0 || window.innerHeight === 0)
      return null;

    const selectedPanelRight = selectedPanel.getBoundingClientRect().right;

    return {
      x: Math.min(0.92, (selectedPanelRight + 1.5 * infoLayout.offsetLeft + 8) / window.innerWidth),
      y: Math.max(0.02, Math.min(0.92, (infoLayout.offsetTop + 8) / window.innerHeight)),
    };
  };

  React.useEffect(() => {
    if (!isOpen)
      return;

    const updateDebug = () => {
      const infoLayout = document.querySelector(`.${infoLayoutTheme.infoLayout}`) as HTMLElement | null;
      const selectedPanel = findSelectedPanel();
      const levelPanel = document.getElementById("level-control-secondary");
      const layoutRect = infoLayout?.getBoundingClientRect();
      const selectedRect = selectedPanel?.getBoundingClientRect();
      const levelRect = levelPanel?.getBoundingClientRect();
      const target = getAlignedPosition();
      const candidates = Array.from(
        document.querySelectorAll<HTMLElement>(`.${nativePanelTheme.panel}.${selectedInfoTheme.selectedInfoPanel}`),
      ).slice(0, 4).map((panel, index) => {
        const rect = panel.getBoundingClientRect();
        return `${index} id=${panel.id || "-"} x=${Math.round(rect.left)}..${Math.round(rect.right)} w=${Math.round(rect.width)}`;
      }).join("\n");

      setPanelDebug([
        `LEVEL CONTROL DEBUG ${window.innerWidth}x${window.innerHeight}`,
        `layout off=${infoLayout?.offsetLeft ?? "?"},${infoLayout?.offsetTop ?? "?"} rect=${Math.round(layoutRect?.left ?? 0)},${Math.round(layoutRect?.top ?? 0)}`,
        `anchor=${selectedPanel?.id || selectedPanel?.tagName || "none"} offW=${selectedPanel?.offsetWidth ?? "?"} rect=${Math.round(selectedRect?.left ?? 0)}..${Math.round(selectedRect?.right ?? 0)}`,
        `target=${target ? `${Math.round(target.x * window.innerWidth)},${Math.round(target.y * window.innerHeight)} (${target.x.toFixed(3)},${target.y.toFixed(3)})` : "none"}`,
        `panel=${levelRect ? `${Math.round(levelRect.left)},${Math.round(levelRect.top)}..${Math.round(levelRect.right)},${Math.round(levelRect.bottom)}` : "not mounted"}`,
        candidates || "No matching panel elements",
      ].join("\n"));
    };

    updateDebug();
    const interval = window.setInterval(updateDebug, 100);
    return () => window.clearInterval(interval);
  }, [isOpen, selectionKey, positionReady]);

  React.useEffect(() => {
    if (!isOpen || !isCurrentSelection)
      return;

    let firstFrame = 0;
    let secondFrame = 0;
    const updatePosition = () => {
      const position = getAlignedPosition();
      if (!position) {
        setMeasuredPosition({ selectionKey, ...panelFallbackPosition });
        return;
      }

      setMeasuredPosition({ selectionKey, ...position });
    };

    firstFrame = window.requestAnimationFrame(() => {
      secondFrame = window.requestAnimationFrame(updatePosition);
    });

    return () => {
      window.cancelAnimationFrame(firstFrame);
      window.cancelAnimationFrame(secondFrame);
    };
  }, [isOpen, isCurrentSelection, selectionKey]);

  return (
    <>
      {!isCurrentSelection ? null : <>
      <PanelSection>
        <PanelSectionRow
          uppercase={true}
          disableFocus={true}
          left={mod.name}
          right={
            <FocusDisabled>
              <ToolButton
                id="level-control-open"
                focusKey={FOCUS_AUTO}
                src={controlIcons.panel}
                selected={isOpen}
                multiSelect={false}
                disabled={false}
                tooltip={translate("Level_Control.UI.OpenLevelControl")}
                className={toolButtonTheme.button}
                onSelect={() => {
                  setMeasuredPosition({ selectionKey: "", ...panelFallbackPosition });
                  panelVisible.update(!isOpen);
                }}
              />
            </FocusDisabled>
          }
        />
      </PanelSection>
      {isOpen && isCurrentSelection && positionReady && <Portal>
        <>
          <Panel
            id="level-control-secondary"
            key={`${selectionKey}:${measuredPosition.x}:${measuredPosition.y}`}
            draggable={true}
            focusKey={FOCUS_AUTO}
            header={translate("Level_Control.UI.PanelTitle")}
            onClose={() => {
              setMeasuredPosition({ selectionKey: "", ...panelFallbackPosition });
              panelVisible.update(false);
            }}
            initialPosition={{ x: measuredPosition.x, y: measuredPosition.y }}
            style={{ width: "420rem", maxWidth: "calc(100vw - 24rem)", maxHeight: "80vh" }}
          >
            <PanelSection>
              <div
                style={{
                  display: "flex",
                  width: "100%",
                  minHeight: "44rem",
                  alignItems: "center",
                  justifyContent: "space-between",
                  boxSizing: "border-box",
                  padding: "4rem 12rem",
                  gap: "12rem",
                }}
              >
                <span style={{ flex: "0 0 auto", whiteSpace: "nowrap" }}>
                  {translate("Level_Control.UI.LevelLabel")}
                </span>
                <FocusDisabled>
                  <div
                    style={{
                      display: "flex",
                      flex: "0 0 auto",
                      alignItems: "center",
                      justifyContent: "flex-end",
                      gap: "4rem",
                      overflow: "visible",
                    }}
                  >
                    {levels.map(level => {
                      const passed = level <= props.targetLevel;

                      return (
                        <Button
                          key={level}
                          variant="flat"
                          focusKey={FOCUS_AUTO}
                          selected={level === props.targetLevel}
                          onSelect={() => trigger(mod.id, "SetLevel", level)}
                          style={{
                            display: "flex",
                            flex: "0 0 34rem",
                            width: "34rem",
                            minWidth: "34rem",
                            maxWidth: "34rem",
                            height: "30rem",
                            padding: 0,
                            borderRadius: "3rem",
                            overflow: "hidden",
                            boxSizing: "border-box",
                          }}
                        >
                          <span
                            style={{
                              width: "100%",
                              height: "100%",
                              display: "flex",
                              alignItems: "center",
                              justifyContent: "center",
                              borderRadius: "3rem",
                              backgroundColor: passed ? "#00ec78" : "rgba(44, 50, 60, 0.78)",
                              color: passed ? "#07150c" : "#f4f4f4",
                              pointerEvents: "none",
                            }}
                          >
                            {level}
                          </span>
                        </Button>
                      );
                    })}
                    <div style={{ display: "flex", flex: "0 0 40rem", width: "40rem", height: "40rem", alignItems: "center", justifyContent: "center", overflow: "visible" }}>
                      <ToolButton
                        id="level-control-lock"
                        focusKey={FOCUS_AUTO}
                        src={props.locked ? controlIcons.locked : controlIcons.unlocked}
                        selected={props.locked}
                        multiSelect={false}
                        disabled={false}
                        tooltip={props.locked ? translate("Level_Control.UI.UnlockLevel") : translate("Level_Control.UI.LockLevel")}
                        className={toolButtonTheme.button}
                        onSelect={() => trigger(mod.id, "ToggleLock")}
                        style={{ width: "36rem", height: "36rem", padding: "3rem", overflow: "visible" }}
                      />
                    </div>
                  </div>
                </FocusDisabled>
              </div>
            </PanelSection>
          </Panel>
          <div
            style={{
              position: "fixed",
              right: "12px",
              bottom: "120px",
              zIndex: 999999,
              padding: "8px 10px",
              background: "rgba(0,0,0,0.92)",
              border: "1px solid #00cfff",
              color: "#ffffff",
              fontFamily: "monospace",
              fontSize: "13px",
              lineHeight: "18px",
              whiteSpace: "pre",
              pointerEvents: "none",
            }}
          >
            {panelDebug}
          </div>
        </>
      </Portal>}
      </>}
    </>
  );
};
