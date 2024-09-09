const connection = new signalR.HubConnectionBuilder().withUrl("/CommonHub").build();

export {connection};