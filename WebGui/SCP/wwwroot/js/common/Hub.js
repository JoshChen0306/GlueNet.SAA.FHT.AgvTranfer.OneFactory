const connection = new signalR.HubConnectionBuilder()
    .withUrl("/CommonHub")
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .build();

export {connection};