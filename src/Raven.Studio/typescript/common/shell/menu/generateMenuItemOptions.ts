/// <reference path="../../../../typings/tsd.d.ts"/>

import database = require("models/resources/database");

interface GenerateMenuItemOptions {
    activeDatabase: KnockoutObservable<database>;
    canExposeConfigOverTheWire: KnockoutObservable<boolean>;
    isGlobalAdmin: KnockoutObservable<boolean>;
}

export = GenerateMenuItemOptions;