// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

contract HashAnchor {
    mapping(string => bytes32) private _anchors;

    event HashAnchored(string indexed externalOrderId, bytes32 anchoredHash);

    function anchorHash(string calldata externalOrderId, bytes32 anchoredHash) external {
        require(_anchors[externalOrderId] == 0, "Hash already anchored for this order");
        _anchors[externalOrderId] = anchoredHash;
        emit HashAnchored(externalOrderId, anchoredHash);
    }

    function getAnchor(string calldata externalOrderId) external view returns (bytes32) {
        return _anchors[externalOrderId];
    }
}
