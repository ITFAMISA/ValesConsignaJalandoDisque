// ============================================
// SCRIPT JAVASCRIPT PARA AUTORIZACIÓN DE VALES CON CONSIGNACIÓN
// ============================================

/**
 * Clase principal para manejar la autorización de vales con separación de consignación
 */
class VoucherAuthorizationManager {
    constructor() {
        this.isValidating = false;
        this.isAuthorizing = false;
        this.normalItems = [];
        this.consignmentItems = [];
        this.currentVoucherId = null;

        this.init();
    }

    /**
     * Inicializar el manager
     */
    init() {
        this.setupEventListeners();
        this.setupValidationHandlers();
        this.initializeTooltips();
    }

    /**
     * Configurar event listeners
     */
    setupEventListeners() {
        // Event listeners para items normales
        $(document).on('input', 'input[name="txtQtyAS"]', (e) => {
            this.validateNormalItemInput(e.target);
            this.updateRowTotal($(e.target).closest('tr'));
        });

        // Event listeners para items de consignación
        $(document).on('input', 'input[name="txtQtyConsignment"]', (e) => {
            this.validateConsignmentItemInput(e.target);
        });

        // Event listener para botón de validación
        $(document).on('click', '[onclick*="validateBeforeAuthorize"]', (e) => {
            e.preventDefault();
            this.validateBeforeAuthorize();
        });

        // Event listener para mostrar info de consignación
        $(document).on('click', 'tr[data-consignment="true"]', (e) => {
            if (!$(e.target).is('input, button')) {
                const idSupply = $(e.currentTarget).find('input[name="txtQtyConsignment"]').data('supply');
                if (idSupply) {
                    this.showConsignmentStockInfo(idSupply);
                }
            }
        });
    }

    /**
     * Configurar validadores en tiempo real
     */
    setupValidationHandlers() {
        // Validación para items normales
        $(document).on('input', 'input[name="txtQtyAS"]', function () {
            const $input = $(this);
            const maxQty = parseFloat($input.data('qtytotal')) || 0;
            const currentQty = parseFloat($input.val()) || 0;

            if (currentQty > maxQty) {
                $input.addClass('is-invalid');
                $input.attr('title', `Cantidad no puede ser mayor a ${maxQty.toFixed(3)}`);
            } else {
                $input.removeClass('is-invalid');
                $input.removeAttr('title');
            }
        });

        // Validación para items de consignación
        $(document).on('input', 'input[name="txtQtyConsignment"]', function () {
            const $input = $(this);
            const maxStock = parseFloat($input.data('maxstock')) || 0;
            const currentQty = parseFloat($input.val()) || 0;

            if (currentQty > maxStock) {
                $input.addClass('is-invalid');
                $input.attr('title', `Cantidad no puede ser mayor al stock de consignación: ${maxStock.toFixed(3)}`);
            } else {
                $input.removeClass('is-invalid');
                $input.removeAttr('title');
            }
        });
    }

    /**
     * Validar input de item normal
     */
    validateNormalItemInput(input) {
        const $input = $(input);
        const maxQty = parseFloat($input.data('qtytotal')) || 0;
        const currentQty = parseFloat($input.val()) || 0;

        if (currentQty > maxQty) {
            $input.addClass('is-invalid');
            this.showValidationMessage($input, `Cantidad excede el máximo disponible: ${maxQty.toFixed(3)}`, 'error');
            return false;
        } else {
            $input.removeClass('is-invalid');
            this.hideValidationMessage($input);
            return true;
        }
    }

    /**
     * Validar input de item de consignación
     */
    validateConsignmentItemInput(input) {
        const $input = $(input);
        const maxStock = parseFloat($input.data('maxstock')) || 0;
        const currentQty = parseFloat($input.val()) || 0;

        if (currentQty > maxStock) {
            $input.addClass('is-invalid');
            this.showValidationMessage($input, `Cantidad excede el stock de consignación: ${maxStock.toFixed(3)}`, 'error');
            return false;
        } else {
            $input.removeClass('is-invalid');
            this.hideValidationMessage($input);
            return true;
        }
    }

    /**
     * Actualizar total de fila
     */
    updateRowTotal(row) {
        const inputs = row.find('input[name="txtQtyAS"]');
        let total = 0;

        inputs.each(function () {
            total += parseFloat($(this).val()) || 0;
        });

        const reqQty = parseFloat(row.data('qtyreq')) || 0;

        // Remover clases previas
        row.removeClass('table-warning table-success');

        if (total > reqQty) {
            row.addClass('table-warning');
            row.attr('title', `Cantidad autorizada (${total.toFixed(3)}) excede la solicitada (${reqQty.toFixed(3)})`);
        } else if (total > 0 && total <= reqQty) {
            row.addClass('table-success');
            row.attr('title', `Cantidad autorizada: ${total.toFixed(3)} de ${reqQty.toFixed(3)} solicitadas`);
        } else {
            row.removeAttr('title');
        }

        return total;
    }

    /**
     * Validar antes de autorizar
     */
    validateBeforeAuthorize() {
        if (this.isValidating) return false;

        this.isValidating = true;
        const validation = this.performFullValidation();
        this.isValidating = false;

        if (validation.hasErrors) {
            this.showValidationErrors(validation.errors);
            return false;
        }

        this.showValidationSuccess(validation.summary);
        return true;
    }

    /**
     * Realizar validación completa
     */
    performFullValidation() {
        const errors = [];
        const warnings = [];
        let hasItems = false;
        let normalItemsCount = 0;
        let consignmentItemsCount = 0;

        // Validar items normales
        const normalRows = $('tr[data-consignment="false"]');
        normalRows.each((index, row) => {
            const $row = $(row);
            const inputs = $row.find('input[name="txtQtyAS"]');
            let hasQtyInRow = false;
            let rowTotal = 0;

            inputs.each((i, input) => {
                const $input = $(input);
                const maxQty = parseFloat($input.data('qtytotal')) || 0;
                const currentQty = parseFloat($input.val()) || 0;

                if (currentQty > 0) {
                    hasItems = true;
                    hasQtyInRow = true;
                    rowTotal += currentQty;

                    if (currentQty > maxQty) {
                        const itemName = $row.find('td:nth-child(2)').text().trim();
                        errors.push(`${itemName}: Cantidad ${currentQty} excede stock disponible ${maxQty}`);
                    }
                }
            });

            if (hasQtyInRow) {
                normalItemsCount++;
                const reqQty = parseFloat($row.data('qtyreq')) || 0;
                if (rowTotal > reqQty) {
                    const itemName = $row.find('td:nth-child(2)').text().trim();
                    warnings.push(`${itemName}: Cantidad autorizada (${rowTotal}) excede la solicitada (${reqQty})`);
                }
            }
        });

        // Validar items de consignación
        const consignmentRows = $('tr[data-consignment="true"]');
        consignmentRows.each((index, row) => {
            const $row = $(row);
            const input = $row.find('input[name="txtQtyConsignment"]')[0];
            const $input = $(input);
            const maxStock = parseFloat($input.data('maxstock')) || 0;
            const currentQty = parseFloat($input.val()) || 0;
            const reqQty = parseFloat($row.data('qtyreq')) || 0;

            if (currentQty > 0) {
                hasItems = true;
                consignmentItemsCount++;

                if (currentQty > maxStock) {
                    const itemName = $row.find('td:nth-child(2)').text().trim();
                    errors.push(`${itemName}: Cantidad ${currentQty} excede stock de consignación ${maxStock}`);
                }

                if (currentQty > reqQty) {
                    const itemName = $row.find('td:nth-child(2)').text().trim();
                    warnings.push(`${itemName}: Cantidad autorizada excede la solicitada`);
                }
            }
        });

        // Verificar que hay al menos un item
        if (!hasItems) {
            errors.push('Debe autorizar al menos un item antes de continuar');
        }

        return {
            hasErrors: errors.length > 0,
            errors: errors,
            warnings: warnings,
            summary: {
                normalItems: normalItemsCount,
                consignmentItems: consignmentItemsCount,
                totalItems: normalItemsCount + consignmentItemsCount
            }
        };
    }

    /**
     * Autorizar vale con consignación
     */
    async authorizeVoucherWithConsignment(idVoucher) {
        if (this.isAuthorizing) return;

        // Validar primero
        if (!this.validateBeforeAuthorize()) {
            return;
        }

        // Confirmar autorización
        const confirmation = await this.showConfirmationDialog(
            '¿Está seguro de autorizar este vale?',
            'Se procesarán por separado los items normales y los de consignación.'
        );

        if (!confirmation) return;

        this.isAuthorizing = true;
        this.showLoadingSpinner('Autorizando vale...');

        try {
            // Recopilar datos
            const authorizationData = this.collectAuthorizationData(idVoucher);

            // Enviar al servidor
            const response = await this.sendAuthorizationRequest(authorizationData);

            if (response.success) {
                this.showSuccessMessage(
                    'Vale autorizado exitosamente',
                    `Se procesaron ${response.data.normalItemsProcessed} items normales y ${response.data.consignmentItemsProcessed} items de consignación.`
                );

                // Redirigir después de 2 segundos
                setTimeout(() => {
                    window.location.href = '/Voucher/Index';
                }, 2000);
            } else {
                this.showErrorMessage('Error al autorizar', response.message);
            }
        } catch (error) {
            this.showErrorMessage('Error de conexión', 'No se pudo procesar la autorización. Intente nuevamente.');
            console.error('Error al autorizar vale:', error);
        } finally {
            this.isAuthorizing = false;
            this.hideLoadingSpinner();
        }
    }

    /**
     * Recopilar datos de autorización
     */
    collectAuthorizationData(idVoucher) {
        const normalItems = [];
        const consignmentItems = [];

        // Recopilar items normales
        const normalRows = $('tr[data-consignment="false"]');
        normalRows.each((index, row) => {
            const $row = $(row);
            const inputs = $row.find('input[name="txtQtyAS"]');
            const storageAllocations = [];
            let totalAuthorized = 0;

            inputs.each((i, input) => {
                const $input = $(input);
                const qty = parseFloat($input.val()) || 0;
                if (qty > 0) {
                    storageAllocations.push({
                        idStorage: parseInt($input.data('storage')),
                        qtyToDeduct: qty
                    });
                    totalAuthorized += qty;
                }
            });

            if (totalAuthorized > 0) {
                normalItems.push({
                    idVoucherDetail: parseInt($row.data('id')),
                    idSupply: parseInt($row.data('supply')),
                    qtyAuthorized: totalAuthorized,
                    storageAllocations: storageAllocations
                });
            }
        });

        // Recopilar items de consignación
        const consignmentRows = $('tr[data-consignment="true"]');
        consignmentRows.each((index, row) => {
            const $row = $(row);
            const input = $row.find('input[name="txtQtyConsignment"]')[0];
            const $input = $(input);
            const qty = parseFloat($input.val()) || 0;

            if (qty > 0) {
                consignmentItems.push({
                    idVoucherDetail: parseInt($row.data('id')),
                    idSupply: parseInt($input.data('supply')),
                    qtyAuthorized: qty
                });
            }
        });

        return {
            idVoucher: idVoucher,
            comment: $('#txtComment').val() || '',
            normalItems: normalItems,
            consignmentItems: consignmentItems
        };
    }

    /**
     * Enviar solicitud de autorización
     */
    async sendAuthorizationRequest(authorizationData) {
        return new Promise((resolve, reject) => {
            $.ajax({
                url: '/Voucher/AuthorizeVoucherWithConsignment',
                method: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(authorizationData),
                success: (response) => resolve(response),
                error: (xhr, status, error) => reject(error)
            });
        });
    }

    /**
     * Mostrar información de stock de consignación
     */
    async showConsignmentStockInfo(idSupply) {
        try {
            this.showLoadingSpinner('Cargando información...');

            const response = await this.getConsignmentStockInfo(idSupply);

            if (response.success) {
                this.displayConsignmentModal(response.data);
            } else {
                this.showErrorMessage('Error', response.message);
            }
        } catch (error) {
            this.showErrorMessage('Error', 'No se pudo obtener la información de consignación.');
        } finally {
            this.hideLoadingSpinner();
        }
    }

    /**
     * Obtener información de stock de consignación
     */
    async getConsignmentStockInfo(idSupply) {
        return new Promise((resolve, reject) => {
            $.ajax({
                url: '/Voucher/GetConsignmentStockInfo',
                method: 'GET',
                data: { idSupply: idSupply },
                success: (response) => resolve(response),
                error: (xhr, status, error) => reject(error)
            });
        });
    }

    /**
     * Mostrar modal de información de consignación
     */
    displayConsignmentModal(data) {
        const statusClass = this.getStockStatusClass(data.status);
        const statusIcon = this.getStockStatusIcon(data.status);

        const modalContent = `
            <div class="modal fade" id="consignmentStockModal" tabindex="-1" aria-labelledby="consignmentStockModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header bg-warning text-dark">
                            <h5 class="modal-title" id="consignmentStockModalLabel">
                                <i class="bi bi-truck"></i> Información de Consignación
                            </h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body">
                            <div class="row mb-3">
                                <div class="col-md-8">
                                    <h6 class="fw-bold">Artículo:</h6>
                                    <div class="d-flex align-items-center">
                                        <span class="badge bg-secondary me-2">${data.microsipKey}</span>
                                        <span>${data.description}</span>
                                    </div>
                                </div>
                                <div class="col-md-4 text-end">
                                    <h6 class="fw-bold">Estado del Stock:</h6>
                                    <span class="badge ${statusClass} fs-6">
                                        <i class="bi ${statusIcon}"></i> ${data.status}
                                    </span>
                                </div>
                            </div>
                            
                            <hr>
                            
                            <div class="row mb-3">
                                <div class="col-md-4 text-center">
                                    <div class="card border-primary">
                                        <div class="card-body">
                                            <h6 class="card-title text-primary">Stock Actual</h6>
                                            <h3 class="text-primary">${data.currentStock.toFixed(2)}</h3>
                                            <small class="text-muted">${data.unitType}</small>
                                        </div>
                                    </div>
                                </div>
                                <div class="col-md-4 text-center">
                                    <div class="card border-warning">
                                        <div class="card-body">
                                            <h6 class="card-title text-warning">Stock Mínimo</h6>
                                            <h4>${data.minimumStock.toFixed(2)}</h4>
                                            <small class="text-muted">${data.unitType}</small>
                                        </div>
                                    </div>
                                </div>
                                <div class="col-md-4 text-center">
                                    <div class="card border-success">
                                        <div class="card-body">
                                            <h6 class="card-title text-success">Stock Máximo</h6>
                                            <h4>${data.maximumStock.toFixed(2)}</h4>
                                            <small class="text-muted">${data.unitType}</small>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            ${data.lastUpdate ? `
                            <div class="alert alert-info">
                                <small>
                                    <i class="bi bi-clock"></i> Última actualización: 
                                    ${new Date(data.lastUpdate).toLocaleString('es-MX')}
                                    ${data.modifiedBy ? ` por ${data.modifiedBy}` : ''}
                                </small>
                            </div>
                            ` : ''}
                            
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="progress">
                                        <div class="progress-bar ${this.getProgressBarClass(data)}" 
                                             style="width: ${this.calculateStockPercentage(data)}%">
                                            ${this.calculateStockPercentage(data).toFixed(1)}%
                                        </div>
                                    </div>
                                    <small class="text-muted">Nivel de stock actual</small>
                                </div>
                                <div class="col-md-6 text-end">
                                    <small class="text-muted">
                                        Disponible: ${(data.currentStock - data.minimumStock).toFixed(2)} ${data.unitType}
                                    </small>
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="bi bi-x-lg"></i> Cerrar
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remover modal anterior si existe
        $('#consignmentStockModal').remove();

        // Agregar y mostrar modal
        $('body').append(modalContent);
        const modal = new bootstrap.Modal(document.getElementById('consignmentStockModal'));
        modal.show();

        // Limpiar modal al cerrarse
        $('#consignmentStockModal').on('hidden.bs.modal', function () {
            $(this).remove();
        });
    }

    /**
     * Mostrar modal de ayuda de consignación
     */
    showConsignmentHelp() {
        const helpModal = `
            <div class="modal fade" id="consignmentHelpModal" tabindex="-1">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header bg-info text-white">
                            <h5 class="modal-title">
                                <i class="bi bi-question-circle"></i> ¿Qué es la consignación?
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="row">
                                <div class="col-md-6">
                                    <div class="card border-primary">
                                        <div class="card-header bg-primary text-white">
                                            <h6 class="mb-0"><i class="bi bi-box"></i> Items Regulares</h6>
                                        </div>
                                        <div class="card-body">
                                            <ul class="list-unstyled">
                                                <li><i class="bi bi-check text-success"></i> Controlados por almacén</li>
                                                <li><i class="bi bi-check text-success"></i> Stock actualizado automáticamente</li>
                                                <li><i class="bi bi-check text-success"></i> Múltiples ubicaciones</li>
                                                <li><i class="bi bi-check text-success"></i> Inventario propio</li>
                                                <li><i class="bi bi-check text-success"></i> Costos directos</li>
                                            </ul>
                                        </div>
                                    </div>
                                </div>
                                <div class="col-md-6">
                                    <div class="card border-warning">
                                        <div class="card-header bg-warning text-dark">
                                            <h6 class="mb-0"><i class="bi bi-truck"></i> Items de Consignación</h6>
                                        </div>
                                        <div class="card-body">
                                            <ul class="list-unstyled">
                                                <li><i class="bi bi-check text-success"></i> Controlados independientemente</li>
                                                <li><i class="bi bi-check text-success"></i> Stock de proveedor</li>
                                                <li><i class="bi bi-check text-success"></i> Una sola ubicación</li>
                                                <li><i class="bi bi-check text-success"></i> Inventario del proveedor</li>
                                                <li><i class="bi bi-check text-success"></i> Pago por consumo</li>
                                            </ul>
                                        </div>
                                    </div>
                                </div>
                            </div>
                            
                            <hr>
                            
                            <div class="alert alert-info">
                                <h6><i class="bi bi-lightbulb"></i> Información importante:</h6>
                                <ul class="mb-0">
                                    <li>Los items de consignación se procesan automáticamente cuando se autoriza el vale</li>
                                    <li>No afectan el inventario regular de la empresa</li>
                                    <li>El proveedor mantiene la propiedad hasta el consumo</li>
                                    <li>Se facturan únicamente las cantidades consumidas</li>
                                </ul>
                            </div>
                            
                            <div class="alert alert-warning">
                                <h6><i class="bi bi-exclamation-triangle"></i> Consideraciones:</h6>
                                <ul class="mb-0">
                                    <li>Verificar disponibilidad antes de autorizar</li>
                                    <li>Los límites mínimos y máximos son controlados por el proveedor</li>
                                    <li>Las cantidades autorizadas no pueden exceder el stock disponible</li>
                                </ul>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-primary" data-bs-dismiss="modal">
                                <i class="bi bi-check2"></i> Entendido
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('#consignmentHelpModal').remove();
        $('body').append(helpModal);
        const modal = new bootstrap.Modal(document.getElementById('consignmentHelpModal'));
        modal.show();

        $('#consignmentHelpModal').on('hidden.bs.modal', function () {
            $(this).remove();
        });
    }

    // ============================================
    // MÉTODOS AUXILIARES
    // ============================================

    /**
     * Obtener clase CSS para estado de stock
     */
    getStockStatusClass(status) {
        switch (status?.toLowerCase()) {
            case 'crítico': return 'bg-danger';
            case 'bajo': return 'bg-warning';
            case 'normal': return 'bg-success';
            default: return 'bg-secondary';
        }
    }

    /**
     * Obtener icono para estado de stock
     */
    getStockStatusIcon(status) {
        switch (status?.toLowerCase()) {
            case 'crítico': return 'bi-exclamation-triangle';
            case 'bajo': return 'bi-exclamation-circle';
            case 'normal': return 'bi-check-circle';
            default: return 'bi-question-circle';
        }
    }

    /**
     * Calcular porcentaje de stock
     */
    calculateStockPercentage(data) {
        const range = data.maximumStock - data.minimumStock;
        if (range <= 0) return 100;

        const available = data.currentStock - data.minimumStock;
        return Math.max(0, Math.min(100, (available / range) * 100));
    }

    /**
     * Obtener clase de barra de progreso
     */
    getProgressBarClass(data) {
        const percentage = this.calculateStockPercentage(data);
        if (percentage < 20) return 'bg-danger';
        if (percentage < 50) return 'bg-warning';
        return 'bg-success';
    }

    /**
     * Inicializar tooltips
     */
    initializeTooltips() {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }

    /**
     * Mostrar spinner de carga
     */
    showLoadingSpinner(message = 'Cargando...') {
        const spinner = `
            <div id="loadingSpinner" class="d-flex justify-content-center align-items-center flex-column" 
                 style="position: fixed; top: 0; left: 0; width: 100%; height: 100%; 
                        background-color: rgba(0,0,0,0.7); z-index: 9999;">
                <div class="spinner-border text-light mb-3" role="status" style="width: 3rem; height: 3rem;">
                    <span class="visually-hidden">Cargando...</span>
                </div>
                <h5 class="text-light">${message}</h5>
            </div>
        `;
        $('#loadingSpinner').remove();
        $('body').append(spinner);
    }

    /**
     * Ocultar spinner de carga
     */
    hideLoadingSpinner() {
        $('#loadingSpinner').remove();
    }

    /**
     * Mostrar mensaje de éxito
     */
    showSuccessMessage(title, message) {
        this.showAlert('success', title, message, 5000);
    }

    /**
     * Mostrar mensaje de error
     */
    showErrorMessage(title, message) {
        this.showAlert('danger', title, message);
    }

    /**
     * Mostrar errores de validación
     */
    showValidationErrors(errors) {
        const errorList = errors.map(error => `<li>${error}</li>`).join('');
        const content = `<ul class="mb-0">${errorList}</ul>`;
        this.showAlert('danger', 'Errores de validación encontrados', content);
    }

    /**
     * Mostrar resumen de validación exitosa
     */
    showValidationSuccess(summary) {
        const content = `
            <div class="d-flex align-items-center">
                <i class="bi bi-check-circle me-2"></i>
                <div>
                    Validación exitosa: ${summary.totalItems} items listos para autorizar
                    (${summary.normalItems} regulares, ${summary.consignmentItems} consignación)
                </div>
            </div>
        `;
        this.showAlert('success', 'Validación completada', content, 3000);
    }

    /**
     * Mostrar alerta
     */
    showAlert(type, title, content, autoHide = null) {
        const alertId = 'alert_' + Date.now();
        const iconClass = type === 'success' ? 'bi-check-circle' :
            type === 'danger' ? 'bi-exclamation-triangle' :
                type === 'warning' ? 'bi-exclamation-triangle' : 'bi-info-circle';

        const alert = `
            <div id="${alertId}" class="alert alert-${type} alert-dismissible fade show shadow" role="alert">
                <h6 class="alert-heading d-flex align-items-center">
                    <i class="bi ${iconClass} me-2"></i>${title}
                </h6>
                ${content}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;

        // Asegurar que existe el contenedor
        if ($('#alertContainer').length === 0) {
            $('body').prepend('<div id="alertContainer" style="position: fixed; top: 20px; right: 20px; z-index: 1050; max-width: 400px;"></div>');
        }

        $('#alertContainer').prepend(alert);

        // Auto-remove si se especifica
        if (autoHide) {
            setTimeout(() => {
                $(`#${alertId}`).alert('close');
            }, autoHide);
        }
    }

    /**
     * Mostrar mensaje de validación
     */
    showValidationMessage(element, message, type) {
        this.hideValidationMessage(element);

        const messageClass = type === 'error' ? 'text-danger' : 'text-warning';
        const messageHtml = `<small class="validation-message ${messageClass}">${message}</small>`;

        element.closest('td').append(messageHtml);
    }

    /**
     * Ocultar mensaje de validación
     */
    hideValidationMessage(element) {
        element.closest('td').find('.validation-message').remove();
    }

    /**
     * Mostrar diálogo de confirmación
     */
    showConfirmationDialog(title, message) {
        return new Promise((resolve) => {
            const confirmed = confirm(`${title}\n\n${message}`);
            resolve(confirmed);
        });
    }
}

// ============================================
// FUNCIONES GLOBALES PARA COMPATIBILIDAD
// ============================================

// Instancia global del manager
let voucherAuthManager = null;

// Inicializar cuando el documento esté listo
$(document).ready(function () {
    voucherAuthManager = new VoucherAuthorizationManager();
});

// Funciones globales para compatibilidad con el código existente
function validateBeforeAuthorize() {
    return voucherAuthManager ? voucherAuthManager.validateBeforeAuthorize() : false;
}

function AuthorizeVoucherWithConsignment(idVoucher) {
    if (voucherAuthManager) {
        voucherAuthManager.authorizeVoucherWithConsignment(idVoucher);
    }
}

function showConsignmentStockInfo(idSupply) {
    if (voucherAuthManager) {
        voucherAuthManager.showConsignmentStockInfo(idSupply);
    }
}

function showConsignmentHelp() {
    if (voucherAuthManager) {
        voucherAuthManager.showConsignmentHelp();
    }
}