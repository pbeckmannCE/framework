﻿
import * as React from 'react'
import { Route } from 'react-router'
import { ajaxPost, ajaxGet } from '../../../Framework/Signum.React/Scripts/Services';
import * as Navigator from '../../../Framework/Signum.React/Scripts/Navigator'
import Typeahead from '../../../Framework/Signum.React/Scripts/Lines/Typeahead'
import * as OmniboxClient from './OmniboxClient'
import {  OmniboxMessage } from './Signum.Entities.Omnibox'

export interface OmniboxAutocompleteProps {
    spanAttrs?: React.HTMLAttributes;
    inputAttrs?: React.HTMLAttributes;
    menuAttrs?: React.HTMLAttributes;
}

export default class OmniboxAutocomplete extends React.Component<OmniboxAutocompleteProps, void>
{
    handleOnSelect = (result: OmniboxClient.OmniboxResult, e: React.SyntheticEvent) => {

        const ke = e as React.KeyboardEvent;

        if (ke.keyCode && ke.keyCode == 9) {
            return OmniboxClient.toString(result);
        }

        const ctrlKey = ke.ctrlKey;
        const promise = OmniboxClient.navigateTo(result);
        if (promise) {
            promise
                .then(url => {
                    if (url) {
                        if (ctrlKey)
                            window.open(url);
                        else
                            Navigator.currentHistory.push(url);
                    }
                }).done();
        }
        this.typeahead.blur();

        return "";
    }

    typeahead: Typeahead;

    render() {

        let inputAttr = this.props.inputAttrs;

        if (inputAttr == undefined)
            inputAttr = {};

        if (inputAttr.placeholder == undefined)
            inputAttr.placeholder = OmniboxMessage.Search.niceToString();


        const result = (
            <Typeahead ref={ta => this.typeahead = ta} getItems={OmniboxClient.API.getResults} 
                renderItem={OmniboxClient.renderItem}
                onSelect={this.handleOnSelect}
                spanAttrs={this.props.spanAttrs}
                inputAttrs={this.props.inputAttrs}
                menuAttrs={this.props.menuAttrs}
                minLength={0}
                ></Typeahead>  
        );

        return result;
    }
}






