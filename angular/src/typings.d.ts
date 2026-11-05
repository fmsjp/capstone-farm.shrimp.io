///<reference path="../node_modules/abp-web-resources/Abp/Framework/scripts/abp.d.ts"/>
///<reference path="../node_modules/abp-web-resources/Abp/Framework/scripts/libs/abp.signalr.d.ts"/>
///<reference path="../node_modules/moment/moment.d.ts"/>

declare var System: any;
declare var Push: any;

declare namespace abp {
    namespace ui {
        function setBusy(elm?: any, text?: any, delay?: any): void;
        function clearBusy(elm?: any, delay?: any): void;
    }
}
