# Receipt posting validation messages

Changed: ReceivingPosting.cs and DeliveryNoteDetails.tsx. No configuration, schema migration or seeding changes are required.

Start the API with `dotnet run --project src/Wros.Api --launch-profile https` and the frontend with `npm --prefix src/wros-web run dev` using the existing local configuration.

1. Save a draft DN with a lot of 50 units allocated as 30 units to a rack. Select Verify & post stock and confirm. Expect a validation notification and a persistent message showing the SKU, product, lot number, received 50 and allocated 30, with instructions and an Edit draft allocations link. The DN remains Draft and inventory is unchanged.
2. Follow the link, change allocations to 30 + 20 across two racks, and save. Post again. Expect Posted status and 50 units added exactly once.
3. With existing stock for the same product, repeat using another DN. Expect inventory product totals to include both receipts' remaining balances.
4. If the draft editor permits an over-allocation, repeat with 60 allocated against 50 received. Expect the same clear rejection identifying 60 allocated and the required total of 50; no inventory change.

Limitations: server validation reports the first invalid lot. Runtime verification requires the owner's configured database and login. A debugger configured to break on thrown CLR exceptions may pause before the existing API exception handler returns HTTP 400; continue execution to see the browser response.

Owner acceptance: verify these scenarios before proceeding with further changes.
