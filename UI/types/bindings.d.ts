declare module "cs2/bindings" {
  export interface SelectedInfoSectionBase {
    group: string;
    tooltipKeys: string[];
    tooltipTags: string[];
  }

  export interface Entity {
    index: number;
    version: number;
  }

  export const selectedInfo: {
    selectedEntity$: {
      value: Entity;
    };
  };
}
