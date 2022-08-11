import React from "react";
import { DisplayModalAction, HideModalAction, HideContextMenuAction } from "../../contexts/operationStateReducer";
import OperationType from "../../contexts/operationType";
import { MenuItem, ListItemIcon } from "@material-ui/core";
import ContextMenu from "./ContextMenu";
import { Server } from "../../models/server";
import Icon from "../../styles/Icons";
import { colors } from "../../styles/Colors";
import { UpdateWellboreWbGeometrysAction } from "../../contexts/navigationStateReducer";
import WbGeometryPropertiesModal, { WbGeometryPropertiesModalProps } from "../Modals/WbGeometryPropertiesModal";
import { PropertiesModalMode } from "../Modals/ModalParts";
import { Typography } from "@equinor/eds-core-react";
import styled from "styled-components";
import { WbGeometryObjectRow } from "../ContentViews/WbGeometrysListView";
import ConfirmModal from "../Modals/ConfirmModal";
import JobService, { JobType } from "../../services/jobService";
import WbGeometryService from "../../services/wbGeometryService";
import ModificationType from "../../contexts/modificationType";

export interface WbGeometryObjectContextMenuProps {
  checkedWbGeometryObjectRows: WbGeometryObjectRow[];
  dispatchOperation: (action: DisplayModalAction | HideContextMenuAction | HideModalAction) => void;
  dispatchNavigation: (action: UpdateWellboreWbGeometrysAction) => void;
  servers: Server[];
  selectedServer: Server;
}

const WbGeometryObjectContextMenu = (props: WbGeometryObjectContextMenuProps): React.ReactElement => {
  const { checkedWbGeometryObjectRows, dispatchOperation, dispatchNavigation } = props;

  const onClickModify = async () => {
    const mode = PropertiesModalMode.Edit;
    const modifyWbGeometryObjectProps: WbGeometryPropertiesModalProps = { mode, wbGeometryObject: checkedWbGeometryObjectRows[0].wbGeometry, dispatchOperation };
    dispatchOperation({ type: OperationType.DisplayModal, payload: <WbGeometryPropertiesModal {...modifyWbGeometryObjectProps} /> });
    dispatchOperation({ type: OperationType.HideContextMenu });
  };

  const deleteWbGeometrys = async () => {
    dispatchOperation({ type: OperationType.HideModal });
    const job = {
      wbGeometryReferences: {
        wbGeometryUids: checkedWbGeometryObjectRows.map((wbGeometry) => wbGeometry.uid),
        wellUid: checkedWbGeometryObjectRows[0].wellUid,
        wellboreUid: checkedWbGeometryObjectRows[0].wellboreUid
      }
    };
    await JobService.orderJob(JobType.DeleteWbGeometrys, job);
    const freshWbGeometrys = await WbGeometryService.getWbGeometrys(checkedWbGeometryObjectRows[0].wbGeometry.wellUid, checkedWbGeometryObjectRows[0].wbGeometry.wellboreUid);
    dispatchNavigation({
      type: ModificationType.UpdateWbGeometryObjects,
      payload: {
        wellUid: job.wbGeometryReferences.wellUid,
        wellboreUid: job.wbGeometryReferences.wellboreUid,
        wbGeometrys: freshWbGeometrys
      }
    });
    dispatchOperation({ type: OperationType.HideContextMenu });
  };

  const onClickDelete = async () => {
    const confirmation = (
      <ConfirmModal
        heading={"Delete wellbore geometrie(s)?"}
        content={
          <span>
            This will permanently delete Wellbore Geometries: <strong>{checkedWbGeometryObjectRows.map((item) => item.uid).join(", ")}</strong>
          </span>
        }
        onConfirm={() => deleteWbGeometrys()}
        confirmColor={"secondary"}
        switchButtonPlaces={true}
      />
    );
    dispatchOperation({ type: OperationType.DisplayModal, payload: confirmation });
  };

  return (
    <ContextMenu
      menuItems={[
        <MenuItem key={"properties"} onClick={onClickModify} disabled={checkedWbGeometryObjectRows.length !== 1}>
          <StyledIcon name="settings" color={colors.interactive.primaryResting} />
          <Typography color={"primary"}>Properties</Typography>
        </MenuItem>,
        <MenuItem key={"delete"} onClick={onClickDelete} disabled={checkedWbGeometryObjectRows.length === 0}>
          <ListItemIcon>
            <StyledIcon name="deleteToTrash" color={colors.interactive.primaryResting} />
          </ListItemIcon>
          <Typography color={"primary"}>Delete</Typography>
        </MenuItem>
      ]}
    />
  );
};
const StyledIcon = styled(Icon)`
  && {
    margin-right: 5px;
  }
`;

export default WbGeometryObjectContextMenu;