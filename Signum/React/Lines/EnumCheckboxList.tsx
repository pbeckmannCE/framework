import * as React from 'react'
import { classes, Dic } from '../Globals'
import { mlistItemContext, TypeContext } from '../TypeContext'
import { getTypeInfo } from '../Reflection'
import { LineBaseController, LineBaseProps, useController } from '../Lines/LineBase'
import { MList, newMListElement } from '../Signum.Entities'
import { EntityCheckboxList } from './EntityCheckboxList'
import { getTimeMachineCheckboxIcon, getTimeMachineIcon } from './TimeMachineIcon'
import { GroupHeader, HeaderType } from './GroupHeader'

export interface EnumCheckboxListProps extends LineBaseProps {
  data?: string[];
  ctx: TypeContext<MList<string>>;
  columnCount?: number;
  columnWidth?: number;
  avoidFieldSet?: boolean | HeaderType;
}

export class EnumCheckboxListController extends LineBaseController<EnumCheckboxListProps> {

  getDefaultProps(p: EnumCheckboxListProps) {
    super.getDefaultProps(p);
    p.columnWidth = 200;
    if (p.type) {
      const ti = getTypeInfo(p.type.name);
      p.data = Dic.getKeys(ti.members);
    }
  }

  handleOnChange = (event: React.ChangeEvent<HTMLInputElement>, val: string) => {
    const current = event.currentTarget;

    var list = this.props.ctx.value;
    var toRemove = list.filter(mle => mle.element == val)

    if (toRemove.length) {
      toRemove.forEach(mle => list.remove(mle));
      this.setValue(list);
    }
    else {
      list.push(newMListElement(val));
      this.setValue(list);
    }
  }

}

export const EnumCheckboxList = React.forwardRef(function EnumCheckboxList(props: EnumCheckboxListProps, ref: React.Ref<EnumCheckboxListController>) {
  const c = useController(EnumCheckboxListController, props, ref);
  const p = c.props;

  if (c.isHidden)
    return null;

  return (
    <GroupHeader className={classes("sf-checkbox-list", p.ctx.errorClassBorder)} 
      label={p.label}
      labelIcon={p.labelIcon}
      avoidFieldSet={p.avoidFieldSet}
      buttons={undefined}
      htmlAttributes={{ ...c.baseHtmlAttributes(), ...p.formGroupHtmlAttributes, ...p.ctx.errorAttributes() }} >
      {renderContent()}
    </GroupHeader >
  );

  if (p.avoidFieldSet == true)
    return (
      <div{...c.baseHtmlAttributes()} {...p.formGroupHtmlAttributes}>
        {renderContent()}
      </div>
    );

  return (
    <fieldset className={classes("sf-checkbox-list", p.ctx.errorClass)} {...c.baseHtmlAttributes()} {...p.formGroupHtmlAttributes}>
      <legend>
        <div>
          <span>{p.label}</span>
        </div>
      </legend>
      {renderContent()}
    </fieldset>
  );


  function renderContent() {
    if (p.data == null)
      return null;

    var data = [...p.data];

    p.ctx.value.forEach(mle => {
      if (!data.some(d => d == mle.element))
        data.insertAt(0, mle.element)
    });

    const ti = getTypeInfo(p.type!.name);

    var listCtx = mlistItemContext(p.ctx);

    return (
      <div className="sf-checkbox-elements" style={getColumnStyle()}>
        {data.map((val, i) => {
          var ectx = listCtx.firstOrNull(ec => ec.value == val);
          var oldCtx = p.ctx.previousVersion == null || p.ctx.previousVersion.value == null ? null :
            listCtx.firstOrNull(el => el.previousVersion?.value == val);

          return (<label className="sf-checkbox-element" key={val}>
            {getTimeMachineCheckboxIcon({ newCtx: ectx, oldCtx: oldCtx, type: ti })}
            <input type="checkbox"
              className="form-check-input"
              checked={p.ctx.value.some(mle => mle.element == val)}
              disabled={p.ctx.readOnly}
              name={val}
              onChange={e => c.handleOnChange(e, val)} />
            &nbsp;
            <span>{ti.members[val].niceName}</span>
          </label>);
        })}
      </div>
    );
  }

  function getColumnStyle(): React.CSSProperties | undefined {

    if (p.columnCount && p.columnWidth)
      return {
        columns: `${p.columnCount} ${p.columnWidth}px`,
      };

    if (p.columnCount)
      return {
        columnCount: p.columnCount,
      };

    if (p.columnWidth)
      return {
        columnWidth: p.columnWidth,
      };

    return undefined;
  }
});
