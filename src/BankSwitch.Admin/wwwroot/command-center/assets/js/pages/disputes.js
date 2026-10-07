import { liveModule } from '../core/live-module.js';
export async function render(root){return liveModule(root,{eyebrow:'Disputes / Chargeback',title:'Dispute & Network Exchange',description:'Live customer disputes and Visa/Mastercard/NPCI ODR/UDIR exchange files.',sources:[{label:'Disputes',path:'/disputes/'},{label:'Network Exchange Files',path:'/disputes/network-exchange/files'}]});}
